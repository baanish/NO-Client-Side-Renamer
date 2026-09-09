using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using ClientSideRenamer.Aliases;

namespace ClientSideRenamer.Remote;

internal sealed class RemoteCsvClient : IDisposable
{
    private readonly HttpClient _client;

    internal RemoteCsvClient() : this(CreateHttpHandler())
    {
    }

    internal static HttpWebRequest CreateHttpRequest(Uri endpoint)
    {
        var request = WebRequest.CreateHttp(endpoint);
        try
        {
            request.ConnectionGroupName = $"ClientSideRenamer-{Guid.NewGuid():N}";
            request.Method = WebRequestMethods.Http.Get;
            request.AllowAutoRedirect = false;
            request.CookieContainer = null;
            request.UseDefaultCredentials = false;
            request.Credentials = null;
            request.Proxy = null;
            request.AutomaticDecompression = DecompressionMethods.None;
            request.MaximumResponseHeadersLength = 16;
            request.KeepAlive = false;
            // Unity Mono's HttpClientHandler callback adapter casts an HttpWebRequest sender to string.
            request.ServerCertificateValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None;
            return request;
        }
        catch
        {
            ReleaseRequest(request, null);
            throw;
        }
    }

    internal static HttpMessageHandler CreateHttpHandler() => new HttpWebRequestMessageHandler();

    internal RemoteCsvClient(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal async Task<AliasSnapshot> FetchAsync(
        string endpoint, string password, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ValidateEndpoint(endpoint));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        if (!string.IsNullOrEmpty(password))
        {
            if (password.Length > 1024)
                throw new FormatException("Remote CSV password exceeds the supported length.");
            foreach (var character in password)
            {
                if (character < ' ' || character > '~')
                    throw new FormatException("Remote CSV password must contain printable ASCII characters only.");
            }
            request.Headers.Add("PASSWORD", password);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);
            using var abortResponse = deadline.Token.Register(response.Dispose);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException("Remote CSV endpoint did not return HTTP 200.");
            if (response.Content == null || response.Content.Headers.ContentEncoding.Any(
                    encoding => !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase))
                || response.Content.Headers.ContentLength > RemoteCsvParser.MaximumBytes)
                throw new HttpRequestException("Remote CSV response is unsupported or too large.");

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (bytes.Length + read > RemoteCsvParser.MaximumBytes)
                    throw new HttpRequestException("Remote CSV response is too large.");
                bytes.Write(buffer, 0, read);
            }
            deadline.Token.ThrowIfCancellationRequested();
            return RemoteCsvParser.Parse(bytes.ToArray());
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException("Remote CSV request was canceled.", token);
            throw new HttpRequestException("Remote CSV request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new HttpRequestException("Remote CSV request failed.");
        }
        catch (IOException)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException("Remote CSV request was canceled.", token);
            if (deadline.IsCancellationRequested)
                throw new HttpRequestException("Remote CSV request timed out.");
            throw new HttpRequestException("Remote CSV response could not be read.");
        }
        catch (ObjectDisposedException)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException("Remote CSV request was canceled.", token);
            if (deadline.IsCancellationRequested)
                throw new HttpRequestException("Remote CSV request timed out.");
            throw new HttpRequestException("Remote CSV response could not be read.");
        }
    }

    private static Uri ValidateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2048)
            throw new FormatException("Remote CSV endpoint must be an absolute HTTPS URL without credentials or a fragment.");
        foreach (var character in endpoint)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
                throw new FormatException("Remote CSV endpoint contains unsupported characters.");
        }
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new FormatException("Remote CSV endpoint must be an absolute HTTPS URL without credentials or a fragment.");
        return uri;
    }

    private static void ReleaseRequest(HttpWebRequest request, WebResponse response)
    {
        try { response?.Close(); }
        catch { }
        try { request.Abort(); }
        catch { }
        if (string.IsNullOrEmpty(request.ConnectionGroupName))
            return;
        try { request.ServicePoint.CloseConnectionGroup(request.ConnectionGroupName); }
        catch { }
    }

    private sealed class HttpWebRequestMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage message, CancellationToken cancellationToken)
        {
            if (message.Method != HttpMethod.Get)
                throw new NotSupportedException("Remote CSV transport supports GET requests only.");
            if (message.RequestUri == null)
                throw new InvalidOperationException("Remote CSV request URI is missing.");

            HttpWebRequest request = null;
            HttpWebResponse response = null;
            var ownershipTransferred = false;
            try
            {
                request = CreateHttpRequest(message.RequestUri);
                if (message.Headers.Accept.Count != 0)
                    request.Accept = message.Headers.Accept.ToString();
                if (message.Headers.TryGetValues("PASSWORD", out var passwords))
                    request.Headers.Add("PASSWORD", string.Join(", ", passwords));

                using (cancellationToken.Register(request.Abort))
                {
                    try
                    {
                        response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                    }
                    catch (WebException exception)
                    {
                        try { exception.Response?.Close(); }
                        catch { }
                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException("Remote CSV request was canceled.", cancellationToken);
                        throw new HttpRequestException("Remote CSV request failed.");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var result = new HttpResponseMessage(response.StatusCode)
                {
                    Content = new HttpWebResponseContent(request, response)
                };
                if (response.ContentLength >= 0)
                    result.Content.Headers.ContentLength = response.ContentLength;
                if (!string.IsNullOrEmpty(response.ContentEncoding))
                    result.Content.Headers.TryAddWithoutValidation("Content-Encoding", response.ContentEncoding);
                ownershipTransferred = true;
                return result;
            }
            finally
            {
                if (!ownershipTransferred && request != null)
                    ReleaseRequest(request, response);
            }
        }
    }

    private sealed class HttpWebResponseContent : HttpContent
    {
        private readonly HttpWebRequest _request;
        private readonly HttpWebResponse _response;
        private int _disposed;

        internal HttpWebResponseContent(HttpWebRequest request, HttpWebResponse response)
        {
            _request = request;
            _response = response;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
            _response.GetResponseStream().CopyToAsync(stream);

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult(_response.GetResponseStream());

        protected override bool TryComputeLength(out long length)
        {
            length = Math.Max(0, _response.ContentLength);
            return _response.ContentLength >= 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
                ReleaseRequest(_request, _response);
            base.Dispose(disposing);
        }
    }

    public void Dispose() => _client.Dispose();
}
