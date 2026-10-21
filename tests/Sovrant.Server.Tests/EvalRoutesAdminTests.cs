using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>Eval code graders run commands from suite files on the server, so only admins can start a run.</summary>
public sealed class EvalRoutesAdminTests(SovrantWebAppFactory factory) : IClassFixture<SovrantWebAppFactory>
{
    [Fact]
    public async Task Members_Cannot_Run_Evals_Admins_Can()
    {
        var users = factory.Services.GetRequiredService<IUserService>();
        var tokens = factory.Services.GetRequiredService<ITokenService>();
        var member = await users.CreateAsync(userId: $"eval-member-{Guid.NewGuid():N}", role: "user");
        var memberToken = (await tokens.IssueAsync(member.UserId, name: "test")).Plaintext;
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await Run(client, memberToken)).StatusCode);
        // An admin gets past the check (no such suite here, so 404).
        Assert.Equal(HttpStatusCode.NotFound, (await Run(client, factory.TestAdminToken)).StatusCode);
    }

    private static Task<HttpResponseMessage> Run(HttpClient client, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/evals/run")
        {
            Content = JsonContent.Create(new { suiteName = $"no-such-suite-{Guid.NewGuid():N}" }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(req);
    }
}
