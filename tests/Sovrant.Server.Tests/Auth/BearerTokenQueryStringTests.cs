using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Server.Auth;
using Sovrant.Api.Config;

namespace Sovrant.Server.Tests.Auth;

/// <summary>
/// Verifies that <see cref="BearerTokenMiddleware"/> accepts tokens
/// from the <c>?access_token=</c> query string for SignalR hub paths.
/// </summary>
public sealed class BearerTokenQueryStringTests
{
    private const string TestToken = "test-token-123";

    [Fact]
    public async Task Hub_Path_With_QueryString_Token_Is_Authenticated()
    {
        using var host = await CreateTestHost();
        var client = host.CreateClient();

        // No Authorization header, but token in query string.
        var response = await client.GetAsync($"/hubs/chat/negotiate?access_token={TestToken}");

        // Should pass auth (404 because the hub isn't actually mapped, but NOT 401).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_Path_Without_Token_Is_Rejected()
    {
        using var host = await CreateTestHost();
        var client = host.CreateClient();

        var response = await client.GetAsync("/hubs/chat/negotiate");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonHub_Path_Ignores_QueryString_Token()
    {
        using var host = await CreateTestHost();
        var client = host.CreateClient();

        // Query string token on a non-hub path should NOT be accepted.
        var response = await client.GetAsync($"/v1/status?access_token={TestToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Header_Token_Still_Works()
    {
        using var host = await CreateTestHost();
        var client = host.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);
        var response = await client.GetAsync("/v1/status");

        // Should pass auth (404 because the route isn't mapped, but NOT 401).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #pragma warning disable ASPDEPR004 // WebHostBuilder is deprecated
    private static Task<TestServer> CreateTestHost()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new CredentialConfig
                {
                    SovrantToken = TestToken,
                });
                services.AddSingleton<BearerTokenMiddleware>();
            })
            .Configure(app =>
            {
                app.UseMiddleware<BearerTokenMiddleware>();
                app.Run(context =>
                {
                    context.Response.StatusCode = 404;
                    return Task.CompletedTask;
                });
            });

        return Task.FromResult(new TestServer(builder));
    }
    #pragma warning restore ASPDEPR004
}
