using System.Net;
using System.Net.Http.Headers;

namespace Sovrant.Api.Tests;

/// <summary>Test double for <see cref="HttpMessageHandler"/> that returns a configured response.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    /// <summary>Creates a handler that returns the provided static response.</summary>
    public FakeHttpMessageHandler(HttpResponseMessage response) =>
        _handler = _ => response;

    /// <summary>Creates a handler that calls the provided factory for each request.</summary>
    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        _handler = handler;

    /// <summary>Creates a 200 OK response with the given JSON body and content type application/json.</summary>
    public static HttpResponseMessage JsonOk(string json)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8)
        };
        resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return resp;
    }

    /// <summary>Creates a 401 Unauthorized response.</summary>
    public static HttpResponseMessage Unauthorized(string body = "{\"error\":{\"type\":\"auth\",\"message\":\"bad key\"}}") =>
        new(HttpStatusCode.Unauthorized) { Content = new StringContent(body) };

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_handler(request));
}
