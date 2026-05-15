using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Server.Webhooks;

namespace Sovrant.Server.Tests.Webhooks;

/// <summary>Tests for <see cref="WebhookCallbackService"/>.</summary>
public sealed class WebhookCallbackServiceTests
{
    [Fact]
    public async Task DeliverAsync_PostsToCallbackUrl()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        var service = new WebhookCallbackService(factory, NullLogger<WebhookCallbackService>.Instance);

        var response = new WebhookResponse
        {
            Success = true,
            Source = "slack",
            UserId = "U1",
            SessionId = "webhook:slack:U1",
            Text = "done",
        };

        await service.DeliverAsync("https://hooks.example.com/cb", response, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://hooks.example.com/cb", handler.LastRequest.RequestUri!.ToString());

        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body);
        Assert.Contains("\"text\":\"done\"", body);
    }

    [Fact]
    public async Task DeliverAsync_NonSuccessStatus_DoesNotThrow()
    {
        var handler = new FakeHandler(HttpStatusCode.InternalServerError);
        var factory = new FakeHttpClientFactory(handler);
        var service = new WebhookCallbackService(factory, NullLogger<WebhookCallbackService>.Instance);

        var response = new WebhookResponse { Success = true };

        // Should not throw — errors are logged, not propagated.
        await service.DeliverAsync("https://hooks.example.com/cb", response, CancellationToken.None);
    }

    [Fact]
    public async Task DeliverAsync_InvalidUrl_DoesNotThrow()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        var service = new WebhookCallbackService(factory, NullLogger<WebhookCallbackService>.Instance);

        var response = new WebhookResponse { Success = true };

        // Invalid URL — should not throw (caught internally).
        await service.DeliverAsync("not-a-url", response, CancellationToken.None);
    }

    [Fact]
    public async Task DeliverAsync_NullCallbackUrl_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        var service = new WebhookCallbackService(factory, NullLogger<WebhookCallbackService>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.DeliverAsync(null!, new WebhookResponse(), CancellationToken.None));
    }

    [Fact]
    public async Task DeliverAsync_NullResponse_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        var factory = new FakeHttpClientFactory(handler);
        var service = new WebhookCallbackService(factory, NullLogger<WebhookCallbackService>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.DeliverAsync("https://hooks.example.com/cb", null!, CancellationToken.None));
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Clone the content so it's still readable after the handler returns.
            LastRequest = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = new StringContent(await request.Content!.ReadAsStringAsync(cancellationToken)),
            };
            return new HttpResponseMessage(_statusCode);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
