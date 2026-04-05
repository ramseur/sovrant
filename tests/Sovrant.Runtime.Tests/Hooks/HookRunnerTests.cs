using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Hooks;

namespace Sovrant.Runtime.Tests.Hooks;

/// <summary>Tests for <see cref="HookRunner"/>.</summary>
public sealed class HookRunnerTests
{
    // Shared empty disabled set
    private static readonly IReadOnlySet<string> s_noneDisabled =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static HookRunner Create(
        IReadOnlyList<HookConfig> hooks,
        HookRunner.HookProfile profile = HookRunner.HookProfile.Strict,
        IReadOnlySet<string>? disabled = null,
        Func<string, int, CancellationToken, Task<int>>? runner = null)
        => new(hooks, profile, disabled ?? s_noneDisabled,
               NullLogger<HookRunner>.Instance, runner);

    // ── No hooks ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoHooks_AlwaysReturnsTrue()
    {
        var sut = Create([]);
        var ctx = new HookContext(HookEvent.PreToolUse, "s1", ToolName: "Bash");
        Assert.True(await sut.RunAsync(HookEvent.PreToolUse, ctx));
    }

    // ── Blocking PreToolUse ──────────────────────────────────────────────────

    [Fact]
    public async Task PreToolUse_StrictProfile_ZeroExit_ReturnsTrue()
    {
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PreToolUse, "validate {tool}"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Strict,
            runner: (_, _, _) => Task.FromResult(0));

        var ctx = new HookContext(HookEvent.PreToolUse, "s1", ToolName: "Bash");
        Assert.True(await sut.RunAsync(HookEvent.PreToolUse, ctx));
    }

    [Fact]
    public async Task PreToolUse_StrictProfile_NonZeroExit_ReturnsFalse()
    {
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PreToolUse, "block-dangerous"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Strict,
            runner: (_, _, _) => Task.FromResult(1));

        var ctx = new HookContext(HookEvent.PreToolUse, "s1", ToolName: "Bash");
        Assert.False(await sut.RunAsync(HookEvent.PreToolUse, ctx));
    }

    [Fact]
    public async Task PreToolUse_FirstHookBlocks_SubsequentHooksNotRun()
    {
        var callCount = 0;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PreToolUse, "first"),
            new(HookEvent.PreToolUse, "second"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Strict,
            runner: (_, _, _) => { callCount++; return Task.FromResult(1); });

        var ctx = new HookContext(HookEvent.PreToolUse, "s1", ToolName: "Bash");
        await sut.RunAsync(HookEvent.PreToolUse, ctx);

        Assert.Equal(1, callCount);  // second hook never runs after first blocks
    }

    // ── Non-blocking events ──────────────────────────────────────────────────

    [Fact]
    public async Task PostToolUse_NonZeroExit_StillReturnsTrue()
    {
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "audit-log"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Strict,
            runner: (_, _, _) => Task.FromResult(99));

        var ctx = new HookContext(HookEvent.PostToolUse, "s1", ToolName: "Edit");
        Assert.True(await sut.RunAsync(HookEvent.PostToolUse, ctx));
    }

    [Fact]
    public async Task Stop_AlwaysReturnsTrue()
    {
        var hooks = new List<HookConfig>
        {
            new(HookEvent.Stop, "notify-done"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Strict,
            runner: (_, _, _) => Task.FromResult(1));

        var ctx = new HookContext(HookEvent.Stop, "s1", StopReason: "end_turn");
        Assert.True(await sut.RunAsync(HookEvent.Stop, ctx));
    }

    // ── Profile filtering ────────────────────────────────────────────────────

    [Fact]
    public async Task MinimalProfile_OnlySessionStartAndEnd_OtherEventSkipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "post-tool"),
            new(HookEvent.Stop, "on-stop"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Minimal,
            runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse, new HookContext(HookEvent.PostToolUse, "s1"));
        await sut.RunAsync(HookEvent.Stop, new HookContext(HookEvent.Stop, "s1"));

        Assert.False(called);
    }

    [Fact]
    public async Task MinimalProfile_SessionStart_Fires()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.SessionStart, "on-start"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Minimal,
            runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.SessionStart, new HookContext(HookEvent.SessionStart, "s1"));

        Assert.True(called);
    }

    [Fact]
    public async Task StandardProfile_PreToolUse_Skipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PreToolUse, "validator"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Standard,
            runner: (_, _, _) => { called = true; return Task.FromResult(1); });

        // Even though hook exits non-zero, standard profile skips PreToolUse entirely.
        var result = await sut.RunAsync(
            HookEvent.PreToolUse,
            new HookContext(HookEvent.PreToolUse, "s1", ToolName: "Bash"));

        Assert.False(called);
        Assert.True(result);  // not blocked — hook was skipped
    }

    [Fact]
    public async Task StandardProfile_PostToolUse_Fires()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "audit"),
        };
        var sut = Create(hooks, HookRunner.HookProfile.Standard,
            runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse, new HookContext(HookEvent.PostToolUse, "s1"));

        Assert.True(called);
    }

    // ── Disabled hooks ───────────────────────────────────────────────────────

    [Fact]
    public async Task DisabledHook_EventSkipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.Stop, "on-stop"),
        };
        var disabled = new HashSet<string>(["Stop"], StringComparer.OrdinalIgnoreCase);
        var sut = Create(hooks, HookRunner.HookProfile.Strict, disabled,
            runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.Stop, new HookContext(HookEvent.Stop, "s1"));

        Assert.False(called);
    }

    [Fact]
    public async Task DisabledHook_CaseInsensitive()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "audit"),
        };
        var disabled = new HashSet<string>(["posttooluse"], StringComparer.OrdinalIgnoreCase);
        var sut = Create(hooks, HookRunner.HookProfile.Strict, disabled,
            runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse, new HookContext(HookEvent.PostToolUse, "s1"));

        Assert.False(called);
    }

    // ── Tool matcher ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolMatcher_MatchingTool_Fires()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "format", Matcher: new HookMatcher(Tool: "Edit")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", ToolName: "Edit"));

        Assert.True(called);
    }

    [Fact]
    public async Task ToolMatcher_NonMatchingTool_Skipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "format", Matcher: new HookMatcher(Tool: "Edit")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", ToolName: "Bash"));

        Assert.False(called);
    }

    [Fact]
    public async Task ToolMatcher_CaseInsensitive()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "format", Matcher: new HookMatcher(Tool: "EDIT")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", ToolName: "edit"));

        Assert.True(called);
    }

    // ── Glob matcher ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GlobMatcher_MatchingExtension_Fires()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "lint", Matcher: new HookMatcher(Glob: "*.cs")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", FilePath: "/src/Foo.cs"));

        Assert.True(called);
    }

    [Fact]
    public async Task GlobMatcher_NonMatchingExtension_Skipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "lint", Matcher: new HookMatcher(Glob: "*.cs")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", FilePath: "/src/Foo.py"));

        Assert.False(called);
    }

    [Fact]
    public async Task GlobMatcher_NoFilePath_Skipped()
    {
        var called = false;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "lint", Matcher: new HookMatcher(Glob: "*.cs")),
        };
        var sut = Create(hooks, runner: (_, _, _) => { called = true; return Task.FromResult(0); });

        // No FilePath in context — glob matcher does not match.
        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", ToolName: "Edit"));

        Assert.False(called);
    }

    // ── Template substitution ────────────────────────────────────────────────

    [Fact]
    public async Task TemplateSubstitution_AllPlaceholdersReplaced()
    {
        string? capturedCommand = null;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse,
                "audit --session={session_id} --tool={tool} --file={file} --is_error={is_error}"),
        };
        var sut = Create(hooks, runner: (cmd, _, _) =>
        {
            capturedCommand = cmd;
            return Task.FromResult(0);
        });

        await sut.RunAsync(HookEvent.PostToolUse, new HookContext(
            HookEvent.PostToolUse,
            SessionId: "abc123",
            ToolName: "Edit",
            FilePath: "/src/Foo.cs",
            IsError: false));

        Assert.Equal(
            "audit --session=abc123 --tool=Edit --file=/src/Foo.cs --is_error=false",
            capturedCommand);
    }

    [Fact]
    public async Task TemplateSubstitution_StopReason()
    {
        string? capturedCommand = null;
        var hooks = new List<HookConfig>
        {
            new(HookEvent.Stop, "notify --reason={stop_reason} --tokens={tokens}"),
        };
        var sut = Create(hooks, runner: (cmd, _, _) =>
        {
            capturedCommand = cmd;
            return Task.FromResult(0);
        });

        await sut.RunAsync(HookEvent.Stop, new HookContext(
            HookEvent.Stop,
            SessionId: "s1",
            StopReason: "end_turn",
            InputTokens: 1234));

        Assert.Equal("notify --reason=end_turn --tokens=1234", capturedCommand);
    }

    // ── Timeout ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timeout_NonBlockingHook_ReturnsTrue()
    {
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "slow-script", TimeoutMs: 1),
        };
        var sut = Create(hooks, runner: async (_, timeoutMs, ct) =>
        {
            await Task.Delay(100, ct);  // outlasts the 1 ms timeout in hook config
            return 0;
        });

        // The mock runner doesn't actually enforce the timeout — the HookRunner passes
        // timeoutMs to the runner which the mock ignores. What we're testing is that
        // a runner that throws OperationCanceledException (non-blocking event) returns true.
        var sut2 = Create(hooks, runner: (_, _, _) =>
            Task.FromException<int>(new OperationCanceledException()));

        // OperationCanceledException from a non-blocking hook is caught internally; always true.
        // (Note: we re-throw for blocking hooks but swallow for non-blocking ones.)
        var ctx = new HookContext(HookEvent.PostToolUse, "s1");
        Assert.True(await sut2.RunAsync(HookEvent.PostToolUse, ctx));
    }

    // ── Multiple hooks ───────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleHooks_AllMatchingFire()
    {
        var callLog = new List<string>();
        var hooks = new List<HookConfig>
        {
            new(HookEvent.Stop, "first"),
            new(HookEvent.Stop, "second"),
            new(HookEvent.Stop, "third"),
        };
        var sut = Create(hooks, runner: (cmd, _, _) =>
        {
            callLog.Add(cmd);
            return Task.FromResult(0);
        });

        await sut.RunAsync(HookEvent.Stop, new HookContext(HookEvent.Stop, "s1"));

        Assert.Equal(["first", "second", "third"], callLog);
    }

    [Fact]
    public async Task MultipleHooks_OnlyMatchingToolFires()
    {
        var callLog = new List<string>();
        var hooks = new List<HookConfig>
        {
            new(HookEvent.PostToolUse, "edit-hook", Matcher: new HookMatcher(Tool: "Edit")),
            new(HookEvent.PostToolUse, "bash-hook", Matcher: new HookMatcher(Tool: "Bash")),
            new(HookEvent.PostToolUse, "all-hook"),
        };
        var sut = Create(hooks, runner: (cmd, _, _) =>
        {
            callLog.Add(cmd);
            return Task.FromResult(0);
        });

        await sut.RunAsync(HookEvent.PostToolUse,
            new HookContext(HookEvent.PostToolUse, "s1", ToolName: "Edit"));

        Assert.Equal(["edit-hook", "all-hook"], callLog);
    }

    // ── ResolveProfile / ResolveDisabled ─────────────────────────────────────

    [Theory]
    [InlineData("minimal", HookRunner.HookProfile.Minimal)]
    [InlineData("MINIMAL", HookRunner.HookProfile.Minimal)]
    [InlineData("strict", HookRunner.HookProfile.Strict)]
    [InlineData("STRICT", HookRunner.HookProfile.Strict)]
    [InlineData("standard", HookRunner.HookProfile.Standard)]
    [InlineData("", HookRunner.HookProfile.Standard)]
    [InlineData(null, HookRunner.HookProfile.Standard)]
    internal void ResolveProfile_EnvVar_ReturnsExpectedProfile(string? value, HookRunner.HookProfile expected)
    {
        var original = Environment.GetEnvironmentVariable("SOVRANT_HOOK_PROFILE");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_HOOK_PROFILE", value);
            Assert.Equal(expected, HookRunner.ResolveProfile());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_HOOK_PROFILE", original);
        }
    }

    [Fact]
    public void ResolveDisabled_CommaSeparated_ReturnsSet()
    {
        var original = Environment.GetEnvironmentVariable("SOVRANT_DISABLED_HOOKS");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_DISABLED_HOOKS", "Stop, PostToolUse, SessionEnd");
            var disabled = HookRunner.ResolveDisabled();

            Assert.Contains("Stop", disabled);
            Assert.Contains("PostToolUse", disabled);
            Assert.Contains("SessionEnd", disabled);
            Assert.DoesNotContain("PreToolUse", disabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_DISABLED_HOOKS", original);
        }
    }
}
