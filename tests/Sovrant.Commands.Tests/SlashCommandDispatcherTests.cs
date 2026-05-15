using Sovrant.Commands;

namespace Sovrant.Commands.Tests;

public sealed class SlashCommandDispatcherTests
{
    // Minimal stub command for testing the dispatcher
    private sealed class StubCommand : ISlashCommand
    {
        public string Name { get; }
        public IReadOnlyList<string> Aliases { get; }
        public string Description => "Stub.";
        public bool WasCalled { get; private set; }
        public string LastArgs { get; private set; } = string.Empty;

        public StubCommand(string name, params string[] aliases)
        {
            Name = name;
            Aliases = aliases;
        }

        public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            WasCalled = true;
            LastArgs = args;
            return Task.FromResult(new SlashCommandResult($"executed:{Name}"));
        }
    }

    private static SlashCommandDispatcher BuildDispatcher(params ISlashCommand[] commands)
    {
        var dispatcher = new SlashCommandDispatcher(commands);
        return dispatcher;
    }

    [Fact]
    public async Task Dispatch_KnownCommand_ExecutesIt()
    {
        var cmd = new StubCommand("test");
        var dispatcher = BuildDispatcher(cmd);

        var result = await dispatcher.TryDispatchAsync("/test");

        Assert.NotNull(result);
        Assert.True(cmd.WasCalled);
        Assert.Contains("executed:test", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatch_KnownCommandWithArgs_PassesArgs()
    {
        var cmd = new StubCommand("foo");
        var dispatcher = BuildDispatcher(cmd);

        await dispatcher.TryDispatchAsync("/foo bar baz");

        Assert.Equal("bar baz", cmd.LastArgs);
    }

    [Fact]
    public async Task Dispatch_Alias_ExecutesPrimaryCommand()
    {
        var cmd = new StubCommand("exit", "quit", "q");
        var dispatcher = BuildDispatcher(cmd);

        var result = await dispatcher.TryDispatchAsync("/quit");

        Assert.NotNull(result);
        Assert.True(cmd.WasCalled);
    }

    [Fact]
    public async Task Dispatch_UnknownCommand_ReturnsErrorMessage()
    {
        var dispatcher = BuildDispatcher();

        var result = await dispatcher.TryDispatchAsync("/unknowncmd");

        Assert.NotNull(result);
        Assert.Contains("Unknown command", result!.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_NonSlashInput_ReturnsNull()
    {
        var dispatcher = BuildDispatcher(new StubCommand("test"));

        var result = await dispatcher.TryDispatchAsync("hello world");

        Assert.Null(result);
    }

    [Fact]
    public async Task Dispatch_EmptySlash_ReturnsHelpHint()
    {
        var dispatcher = BuildDispatcher();

        var result = await dispatcher.TryDispatchAsync("/");

        Assert.NotNull(result);
        Assert.Contains("/help", result!.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_CaseInsensitive()
    {
        var cmd = new StubCommand("UPPER");
        var dispatcher = BuildDispatcher(cmd);

        var result = await dispatcher.TryDispatchAsync("/upper");

        Assert.NotNull(result);
        Assert.True(cmd.WasCalled);
    }

    [Fact]
    public void Commands_ListsDeduplicated()
    {
        var cmd = new StubCommand("multi", "alias1", "alias2");
        var dispatcher = BuildDispatcher(cmd);

        // Should list once, not once per alias
        Assert.Single(dispatcher.Commands);
    }
}
