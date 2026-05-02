using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.TrustBoundary;
using Sovrant.Runtime.Tests.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Workspaces;

/// <summary>
/// End-to-end coverage of the Bucket-B step 4 fan-out path:
/// Settings UI calls <c>SaveToStoreAsync</c>, then <see cref="LiveSettingsRegistry.ReloadAll"/>;
/// the next call into a live consumer (<see cref="GovernanceMonitor"/>,
/// <see cref="PromptSanitizer"/>) reflects the new value without a restart.
/// </summary>
public sealed class HotReloadEndToEndTests
{
    [Fact]
    public async Task GovernanceMonitor_ReflectsStoreUpdateAfterReloadAll()
    {
        var store = new InMemoryStore();
        var live = new LiveSettings<GovernanceConfig>(
            () => GovernanceConfig.Load(workingDirectory: null, settings: store));
        var registry = new LiveSettingsRegistry();
        registry.Register(live);

        using var monitor = new GovernanceMonitor(live, NullAuditStore.Instance, NullLogger<GovernanceMonitor>.Instance);

        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "deploy --to-prod");
        Assert.Equal(GovernanceAction.Allow, (await monitor.EvaluateAsync(ctx)).Action);

        var fresh = new GovernanceConfig
        {
            GovernanceLevelName = nameof(GovernanceLevel.Strict),
            AuditLog = false,
        };
        fresh.BlockedCommands.Add("deploy --to-prod");
        await fresh.SaveToStoreAsync(store);
        registry.ReloadAll();

        var verdict = await monitor.EvaluateAsync(ctx);
        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    [Fact]
    public async Task PromptSanitizer_ReflectsStoreUpdateAfterReloadAll()
    {
        var store = new InMemoryStore();
        var live = new LiveSettings<TrustBoundaryConfig>(
            () => TrustBoundaryConfig.Resolve(new TrustBoundaryConfig { Enabled = true }, store));
        var registry = new LiveSettingsRegistry();
        registry.Register(live);

        using var sanitizer = new PromptSanitizer(live);

        var request = new Sovrant.Api.Types.MessagesRequest("gpt-4o-mini", 1024,
            [Sovrant.Api.Types.InputMessage.UserText("Codename: Falcon ships next week")]);

        var before = ((Sovrant.Api.Types.InputContentBlock.TextBlock)
            sanitizer.Sanitize(request).Request.Messages[0].Content[0]).Text;
        Assert.Contains("Falcon", before);

        var fresh = new TrustBoundaryConfig
        {
            Enabled = true,
            Sanitizer = new SanitizerConfig
            {
                Enabled = true,
                CustomPatterns = [new CustomPattern("CODENAMES", @"Falcon", "redact")],
            },
        };
        await fresh.SaveToStoreAsync(store);
        registry.ReloadAll();

        var after = ((Sovrant.Api.Types.InputContentBlock.TextBlock)
            sanitizer.Sanitize(request).Request.Messages[0].Content[0]).Text;
        Assert.DoesNotContain("Falcon", after);
        Assert.Contains("[CODENAMES_1]", after);
    }

    private sealed class InMemoryStore : IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _global = new(StringComparer.Ordinal);
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_global.TryGetValue(key, out var v) ? v : null);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default)
            => GetGlobalAsync(key, ct);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default)
        {
            _global[key] = value;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default)
        {
            _global.Remove(key);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_global, StringComparer.Ordinal));
    }
}
