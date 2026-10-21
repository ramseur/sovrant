using Sovrant.Commands;
using Sovrant.Runtime.Auth;

namespace Sovrant.Commands.Tests;

/// <summary>
/// On Web, members can't run slash commands that change things for everyone or touch the server's
/// disk (/eval runs commands from suite files, /artifacts can export to any path, …). Admins can;
/// Desktop and the CLI register no policy.
/// </summary>
public sealed class SharedServerCommandPolicyTests
{
    [Theory]
    [InlineData("/eval run smoke")]
    [InlineData("/memory")]
    [InlineData("/artifacts export a1 C:/Windows/x.zip")]
    [InlineData("/swarm on")]
    [InlineData("/websearch off")]
    [InlineData("/provider openai")]
    [InlineData("/EVAL")]
    public async Task Members_Are_Refused_Without_Running_The_Command(string input)
    {
        var cmd = new Recording(input.TrimStart('/').Split(' ')[0].ToLowerInvariant());
        var dispatcher = new SlashCommandDispatcher([cmd], new SharedServerCommandPolicy(AmbientPrincipal.Accessor));

        using (AmbientPrincipal.Push("member@example.com", "user"))
        {
            var result = await dispatcher.TryDispatchAsync(input);
            Assert.Contains("for admins", result!.Output, StringComparison.Ordinal);
        }
        Assert.False(cmd.Ran);
    }

    [Fact]
    public async Task Admins_Can_Run_Them_And_Members_Keep_Everything_Else()
    {
        var eval = new Recording("eval");
        var help = new Recording("cost");
        var dispatcher = new SlashCommandDispatcher([eval, help], new SharedServerCommandPolicy(AmbientPrincipal.Accessor));

        using (AmbientPrincipal.Push("admin@example.com", "admin"))
            await dispatcher.TryDispatchAsync("/eval run smoke");
        using (AmbientPrincipal.Push("member@example.com", "user"))
            await dispatcher.TryDispatchAsync("/cost");

        Assert.True(eval.Ran);
        Assert.True(help.Ran);
    }

    [Fact]
    public async Task Without_A_Policy_Nothing_Changes()
    {
        var eval = new Recording("eval");
        using (AmbientPrincipal.Push("member@example.com", "user"))
            await new SlashCommandDispatcher([eval]).TryDispatchAsync("/eval run smoke");
        Assert.True(eval.Ran);
    }

    private sealed class Recording(string name) : ISlashCommand
    {
        public bool Ran { get; private set; }
        public string Name => name;
        public IReadOnlyList<string> Aliases => [];
        public string Description => "test";
        public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            Ran = true;
            return Task.FromResult(new SlashCommandResult("ran"));
        }
    }
}
