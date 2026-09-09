using System.Net;
using System.Text;
using ClientSideRenamer.Remote;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class RemoteCsvClientTests
{
    private const string Csv = "PlayerName,SteamID\nPilot,76561198000000001";
    private const string Endpoint = "https://roster.example/players.csv";

    [Theory]
    [InlineData("", null)]
    [InlineData("abc.def_123==", "abc.def_123==")]
    [InlineData("pass word", "pass word")]
    public async Task Sends_only_explicit_password_header_and_get(string password, string? expected)
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(Endpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(expected, request.Headers.TryGetValues("PASSWORD", out var values) ? Assert.Single(values) : null);
            Assert.Null(request.Headers.Authorization);
            Assert.Null(request.Content);
            Assert.Empty(request.Headers.AcceptEncoding);
            Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(Response());
        });
        using var client = new RemoteCsvClient(handler);
        Assert.Equal(1, (await client.FetchAsync(Endpoint, password, CancellationToken.None)).Count);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("http://roster.example/players.csv")]
    [InlineData("file:///tmp/players.csv")]
    [InlineData("/players.csv")]
    [InlineData("https://username:password@roster.example/players.csv")]
    [InlineData("https://roster.example/players.csv#fragment")]
    [InlineData("https://roster.example/players.csv\r\nInjected: secret")]
    [InlineData(" https://roster.example/players.csv")]
    public async Task Unsafe_endpoint_is_rejected_before_network_access(string endpoint)
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Must not send"));
        using var client = new RemoteCsvClient(handler);
        await Assert.ThrowsAsync<FormatException>(() => client.FetchAsync(endpoint, "", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("secret\r\nInjected: x")]
    [InlineData("secret\t")]
    [InlineData("secret\0")]
    [InlineData("pässword")]
    public async Task Invalid_password_never_reaches_network(string password)
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Must not send"));
        using var client = new RemoteCsvClient(handler);
        var error = await Assert.ThrowsAsync<FormatException>(() => client.FetchAsync(Endpoint, password, CancellationToken.None));
        Assert.DoesNotContain("Injected", error.ToString());
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Url_and_password_limits_accept_the_boundary_and_reject_excess_before_sending()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(2048, request.RequestUri!.AbsoluteUri.Length);
            Assert.Equal(1024, Assert.Single(request.Headers.GetValues("PASSWORD")).Length);
            return Task.FromResult(Response());
        });
        using var client = new RemoteCsvClient(handler);
        var endpoint = Endpoint + new string('x', 2048 - Endpoint.Length);
        var password = new string('p', 1024);
        Assert.Equal(1, (await client.FetchAsync(endpoint, password, CancellationToken.None)).Count);
        await Assert.ThrowsAsync<FormatException>(() => client.FetchAsync(endpoint + "x", password, CancellationToken.None));
        await Assert.ThrowsAsync<FormatException>(() => client.FetchAsync(endpoint, password + "p", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.PartialContent)]
    public async Task Rejects_redirects_and_every_non_200_without_a_second_request(HttpStatusCode status)
    {
        using var handler = new StubHandler((_, _) =>
        {
            var response = Response();
            response.StatusCode = status;
            response.Headers.Location = new Uri("https://untrusted.example/steal");
            return Task.FromResult(response);
        });
        using var client = new RemoteCsvClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, "secret", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Unknown_length_body_is_stopped_at_the_byte_limit()
    {
        using var stream = new ResponseStream(RemoteCsvParser.MaximumBytes + 100000);
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));
        using var client = new RemoteCsvClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, "", CancellationToken.None));
        Assert.InRange(stream.BytesRead, 1048577, 1048576 + 8192);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Declared_oversize_or_encoded_body_is_rejected_before_reading(bool oversized)
    {
        using var stream = new ResponseStream(10);
        using var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            if (oversized)
                response.Content.Headers.ContentLength = 1048577;
            else
                response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        });
        using var client = new RemoteCsvClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, "", CancellationToken.None));
        Assert.Equal(0, stream.BytesRead);
    }

    [Theory]
    [InlineData("identity", null)]
    [InlineData("IDENTITY", null)]
    [InlineData("identity", "identity")]
    public async Task Identity_content_encoding_is_accepted(string encoding, string? secondEncoding)
    {
        using var handler = new StubHandler((_, _) =>
        {
            var response = Response();
            response.Content.Headers.ContentEncoding.Add(encoding);
            if (secondEncoding != null)
                response.Content.Headers.ContentEncoding.Add(secondEncoding);
            return Task.FromResult(response);
        });
        using var client = new RemoteCsvClient(handler);

        Assert.Equal(1, (await client.FetchAsync(Endpoint, "", CancellationToken.None)).Count);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("unknown")]
    [InlineData("identity, gzip")]
    [InlineData("br, identity")]
    public async Task Compressed_unknown_or_mixed_content_encoding_is_rejected(string encoding)
    {
        using var stream = new ResponseStream(10);
        using var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.TryAddWithoutValidation("Content-Encoding", encoding);
            return Task.FromResult(response);
        });
        using var client = new RemoteCsvClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, "", CancellationToken.None));
        Assert.Equal(0, stream.BytesRead);
    }

    [Fact]
    public async Task Caller_cancellation_reaches_body_reads()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new ResponseStream(0, true);
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));
        using var client = new RemoteCsvClient(handler);
        var fetch = client.FetchAsync(Endpoint, "", cancellation.Token);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Whole_body_has_a_ten_second_deadline()
    {
        using var stream = new ResponseStream(0, true);
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        }));
        using var client = new RemoteCsvClient(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.FetchAsync(Endpoint, "", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal("Remote CSV request timed out.", error.Message);
    }

    [Fact]
    public async Task Transport_failures_do_not_expose_endpoint_credentials_or_response_details()
    {
        using var handler = new StubHandler((_, _) => throw new HttpRequestException("secret https://private.example?password=x"));
        using var client = new RemoteCsvClient(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(Endpoint, "secret", CancellationToken.None));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("secret", error.ToString());
        Assert.DoesNotContain("private.example", error.ToString());
    }

    private static HttpResponseMessage Response() => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Csv))
    };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }

    private sealed class ResponseStream(int size, bool stall = false) : Stream
    {
        internal int BytesRead { get; private set; }
        internal TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            if (stall)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var read = Math.Min(count, size - BytesRead);
            Array.Fill(buffer, (byte)'x', offset, read);
            BytesRead += read;
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
