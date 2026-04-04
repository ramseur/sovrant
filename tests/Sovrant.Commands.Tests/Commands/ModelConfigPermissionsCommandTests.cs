using Sovrant.Commands.Commands;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Commands.Tests.Commands;

public sealed class ModelConfigPermissionsCommandTests
{
    private static SovrantConfig DefaultConfig() => new()
    {
        Model = "test-model-7",
        MaxTokens = 4096,
        PermissionMode = PermissionMode.Default,
    };

    // ── ModelCommand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Model_ShowsCurrentModel()
    {
        var cmd = new ModelCommand(DefaultConfig());
        var result = await cmd.ExecuteAsync(string.Empty);
        Assert.Contains("test-model-7", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_WithArgShowsRestartHint()
    {
        var cmd = new ModelCommand(DefaultConfig());
        var result = await cmd.ExecuteAsync("gpt-4o");
        Assert.Contains("sovrant --model gpt-4o", result.Output, StringComparison.Ordinal);
    }

    // ── ConfigCommand ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Config_ShowsAllFields()
    {
        var cmd = new ConfigCommand(DefaultConfig());
        var result = await cmd.ExecuteAsync(string.Empty);
        Assert.NotNull(result.Output);
        Assert.Contains("test-model-7", result.Output, StringComparison.Ordinal);
        Assert.Contains("4096", result.Output, StringComparison.Ordinal);
        Assert.Contains("Default", result.Output, StringComparison.Ordinal);
    }

    // ── PermissionsCommand ─────────────────────────────────────────────────────

    [Fact]
    public async Task Permissions_ListsAllModesAndMarksActive()
    {
        var cmd = new PermissionsCommand(DefaultConfig());
        var result = await cmd.ExecuteAsync(string.Empty);
        Assert.NotNull(result.Output);
        // All modes should be listed
        foreach (var mode in Enum.GetValues<PermissionMode>())
            Assert.Contains(mode.ToString(), result.Output, StringComparison.Ordinal);
        // Active mode should be marked
        Assert.Contains("◀ active", result.Output, StringComparison.Ordinal);
    }
}
