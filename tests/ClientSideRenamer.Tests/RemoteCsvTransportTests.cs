using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ClientSideRenamer.Remote;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class RemoteCsvTransportTests
{
    [Theory]
    [InlineData(SslPolicyErrors.None, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateChainErrors, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch
        | SslPolicyErrors.RemoteCertificateChainErrors, false)]
    public void Default_transport_accepts_only_certificates_without_policy_errors(
        SslPolicyErrors errors, bool expected)
    {
        var request = RemoteCsvClient.CreateHttpRequest(new Uri("https://roster.example/players.csv"));
        try
        {
            var validate = request.ServerCertificateValidationCallback;

            Assert.NotNull(validate);
            Assert.Equal(expected, validate(request, null!, null!, errors));
        }
        finally
        {
            Close(request);
        }
    }

    [Fact]
    public void Default_requests_disable_ambient_state_and_use_distinct_connection_groups()
    {
        var first = RemoteCsvClient.CreateHttpRequest(new Uri("https://roster.example/first.csv"));
        var second = RemoteCsvClient.CreateHttpRequest(new Uri("https://roster.example/second.csv"));
        try
        {
            Assert.Equal(WebRequestMethods.Http.Get, first.Method);
            Assert.False(first.AllowAutoRedirect);
            Assert.Null(first.CookieContainer);
            Assert.False(first.UseDefaultCredentials);
            Assert.Null(first.Credentials);
            Assert.Null(first.Proxy);
            Assert.Equal(DecompressionMethods.None, first.AutomaticDecompression);
            Assert.Equal(16, first.MaximumResponseHeadersLength);
            Assert.False(first.KeepAlive);
            Assert.StartsWith("ClientSideRenamer-", first.ConnectionGroupName);
            Assert.NotEqual(first.ConnectionGroupName, second.ConnectionGroupName);
        }
        finally
        {
            Close(first);
            Close(second);
        }
    }

    [Fact]
    public async Task Adapter_forwards_request_and_response_headers_and_closes_its_connection()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        using var handler = RemoteCsvClient.CreateHttpHandler();
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/roster.csv");
        request.Headers.Accept.ParseAdd("text/csv");
        request.Headers.Add("PASSWORD", "private-roster-token");
        var pending = invoker.SendAsync(request, deadline.Token);

        using var peer = await accepted;
        using var stream = peer.GetStream();
        var requestHeaders = await ReadHeadersAsync(stream, deadline.Token);
        Assert.Contains("GET /roster.csv HTTP/1.1\r\n", requestHeaders);
        Assert.Contains("Accept: text/csv\r\n", requestHeaders);
        Assert.Contains("PASSWORD: private-roster-token\r\n", requestHeaders);
        await WriteAsync(stream,
            "HTTP/1.1 200 OK\r\nContent-Length: 4\r\nContent-Encoding: identity\r\n\r\nbody",
            deadline.Token);

        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, response.Content.Headers.ContentLength);
        Assert.Equal("identity", Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Equal("body", await response.Content.ReadAsStringAsync(deadline.Token));
        response.Dispose();
        Assert.Equal(0, await stream.ReadAsync(new byte[1], deadline.Token));
    }

    [Fact]
    public async Task Adapter_aborts_header_fetch_when_canceled()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        using var handler = RemoteCsvClient.CreateHttpHandler();
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/roster.csv");
        var pending = invoker.SendAsync(request, cancellation.Token);

        using var peer = await accepted;
        using var stream = peer.GetStream();
        await ReadHeadersAsync(stream, deadline.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, await stream.ReadAsync(new byte[1], deadline.Token));
    }

    [Fact]
    public async Task Adapter_closes_error_response_and_hides_web_exception_details()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        using var handler = RemoteCsvClient.CreateHttpHandler();
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/roster.csv");
        var pending = invoker.SendAsync(request, deadline.Token);

        using var peer = await accepted;
        using var stream = peer.GetStream();
        await ReadHeadersAsync(stream, deadline.Token);
        await WriteAsync(stream,
            "HTTP/1.1 500 private-token\r\nContent-Length: 100\r\n\r\n",
            deadline.Token);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => pending);
        Assert.Equal("Remote CSV request failed.", error.Message);
        Assert.Null(error.InnerException);
        Assert.Equal(0, await stream.ReadAsync(new byte[1], deadline.Token));
    }

    [Fact]
    public async Task Default_transport_rejects_untrusted_certificate_before_sending_password()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        using var client = new RemoteCsvClient();
        var fetch = client.FetchAsync($"https://127.0.0.1:{port}/roster.csv", "private-roster-token", deadline.Token);

        using var peer = await accepted;
        using var stream = new SslStream(peer.GetStream());
        var receivedBytes = 0;
        try
        {
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate
            }, deadline.Token);
            receivedBytes = await stream.ReadAsync(new byte[4096], deadline.Token);
        }
        catch (AuthenticationException) { }
        catch (IOException) { }

        await Assert.ThrowsAsync<HttpRequestException>(() => fetch);
        Assert.Equal(0, receivedBytes);
    }

    private static void Close(HttpWebRequest request)
    {
        request.Abort();
        request.ServicePoint.CloseConnectionGroup(request.ConnectionGroupName!);
    }

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken token)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[1];
        while (bytes.Length < 16384)
        {
            Assert.Equal(1, await stream.ReadAsync(buffer, token));
            bytes.WriteByte(buffer[0]);
            if (bytes.Length >= 4)
            {
                var data = bytes.GetBuffer();
                var end = (int)bytes.Length;
                if (data[end - 4] == '\r' && data[end - 3] == '\n'
                    && data[end - 2] == '\r' && data[end - 1] == '\n')
                    return Encoding.ASCII.GetString(data, 0, end);
            }
        }

        throw new InvalidDataException("HTTP request headers exceeded the test limit.");
    }

    private static Task WriteAsync(Stream stream, string value, CancellationToken token) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(value), token).AsTask();
}
