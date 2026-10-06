using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Tools;
using Sovrant.Runtime.Workspaces;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 139 — the Desktop Integrations page and top bar show an unavailable MCP server's reason,
/// what happens next, and the right buttons (Retry now for network failures; Update key + Retry for
/// rejected credentials). Rendered headlessly with the real view and view model.
/// </summary>
public sealed class McpUnavailableRenderTests
{
    private static readonly McpServerConfig Dns = new() { Url = new Uri("https://does-not-exist.invalid/mcp") };
    private static readonly McpServerConfig Key = new() { Url = new Uri("http://127.0.0.1:8765/mcp") };

    [AvaloniaFact]
    public async Task Integrations_Shows_Reason_NextStep_And_Buttons_Per_Failure_Kind()
    {
        var status = new McpServerStatusRegistry();
        status.Set(new McpServerStatus("brokendns", McpServerState.Unavailable,
            McpConnectionError.Classify("brokendns", Dns, new InvalidOperationException("No such host is known.")),
            NextRetryAt: DateTimeOffset.UtcNow.AddSeconds(50), Attempt: 1, MaxAttempts: 3));
        status.Set(new McpServerStatus("badkey", McpServerState.Unavailable,
            McpConnectionError.Classify("badkey", Key, new System.Net.Http.HttpRequestException("denied", null, System.Net.HttpStatusCode.Unauthorized))));

        var store = Empty<IMcpServerStore>.With(nameof(IMcpServerStore.GetAllAsync),
            Task.FromResult<IReadOnlyDictionary<string, McpServerConfig>>(new Dictionary<string, McpServerConfig> { ["brokendns"] = Dns, ["badkey"] = Key }));
        var clients = new McpClientRegistry();
        var registrar = new McpToolRegistrar(Empty<IMcpClientFactory>.Create(), new InMemoryToolRegistry(), clients, status, NullLogger<McpToolRegistrar>.Instance);
        var oauth = new McpOAuthService(store, Empty<ICredentialStore>.Create(), registrar, Empty<IHttpClientFactory>.Create(), NullLogger<McpOAuthService>.Instance);
        var vm = new IntegrationsViewModel(store, clients, registrar, oauth, Empty<IMcpTrustRuleStore>.Create(),
            Empty<IWorkspaceService>.Create(), Empty<IPrincipalAccessor>.Create(), mcpStatus: status);
        vm.SwitchTabCommand.Execute("connected");
        for (var i = 0; i < 50 && vm.FilteredServers.Count < 2; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.Equal(2, vm.FilteredServers.Count);

        var window = new Window { Width = 1300, Height = 900, Content = new IntegrationsView { DataContext = vm } };
        window.Show();
        try
        {
            vm.SelectServerCommand.Execute(vm.FilteredServers.First(s => s.Name == "brokendns"));
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
            var texts = VisibleTexts(window);
            Assert.Contains("Couldn't reach does-not-exist.invalid", texts);
            Assert.Contains("Check your internet connection. Retrying automatically.", texts);
            Assert.Contains(texts, t => t.StartsWith("Next attempt in", StringComparison.Ordinal));
            Assert.Contains("Retry now", texts);
            Assert.DoesNotContain("Update key", VisibleButtons(window));
            Assert.Contains("Unavailable · retrying", texts);
            Save(window, "desktop-dns.png");

            vm.SelectServerCommand.Execute(vm.FilteredServers.First(s => s.Name == "badkey"));
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
            texts = VisibleTexts(window);
            Assert.Contains("badkey rejected the credentials", texts);
            Assert.Contains("Not retried automatically: retrying can't fix a key.", texts);
            Assert.Contains("Update key", VisibleButtons(window));
            Assert.Contains("Retry", texts);
            Save(window, "desktop-key.png");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Top_Bar_Warns_With_The_Most_Serious_Reason()
    {
        var network = new McpServerToggleItem("brokendns", true, () => Task.CompletedTask)
        {
            Status = new McpServerStatus("brokendns", McpServerState.Unavailable,
                McpConnectionError.Classify("brokendns", Dns, new InvalidOperationException("No such host is known.")), NextRetryAt: DateTimeOffset.UtcNow),
        };
        var key = new McpServerToggleItem("badkey", true, () => Task.CompletedTask)
        {
            Status = new McpServerStatus("badkey", McpServerState.Unavailable,
                McpConnectionError.Classify("badkey", Key, new System.Net.Http.HttpRequestException("x", null, System.Net.HttpStatusCode.Forbidden))),
        };
        var ok = new McpServerToggleItem("github", true, () => Task.CompletedTask);

        Assert.True(network.IsNetworkWarning);
        Assert.True(key.IsCredentialWarning);
        Assert.False(ok.HasWarning);
        Assert.Equal("Couldn't reach does-not-exist.invalid. Check your internet connection. Retrying automatically.", network.WarningText);

        var context = new ActiveContextViewModel(Empty<IWorkspaceService>.Create(), Empty<Sovrant.Runtime.Projects.IProjectService>.Create(), Empty<IPrincipalAccessor>.Create());
        context.AvailableMcpServers.Add(ok);
        context.AvailableMcpServers.Add(network);
        Assert.True(context.McpWarningIsNetwork);
        context.AvailableMcpServers.Add(key);
        Assert.True(context.McpWarningIsCredential); // credentials beat network
        Assert.StartsWith("badkey rejected the credentials", context.McpWarningText, StringComparison.Ordinal);
    }

    private static List<string> VisibleTexts(Window w) =>
        w.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!).ToList();

    private static List<string> VisibleButtons(Window w) =>
        w.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Content is string).Select(b => (string)b.Content!).ToList();

    private static void Save(Window w, string name)
    {
        if (Environment.GetEnvironmentVariable("SOVRANT_UI_SHOT_DIR") is { Length: > 0 } dir)
            w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }
}

/// <summary>A do-nothing implementation of any interface: tasks complete with empty/default results.</summary>
public class Empty<T> : DispatchProxy where T : class
{
    private Dictionary<string, object?> _overrides = new(StringComparer.Ordinal);

    public static T Create() => With(null, null);

    public static T With(string? method, object? result)
    {
        var proxy = Create<T, Empty<T>>();
        if (method is not null)
            ((Empty<T>)(object)proxy)._overrides[method] = result;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        if (_overrides.TryGetValue(targetMethod.Name, out var value)) return value;
        var type = targetMethod.ReturnType;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type == typeof(ValueTask)) return ValueTask.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            var result = EmptyValue(inner);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [result]);
        }
        return EmptyValue(type);
    }

    private static object? EmptyValue(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (def == typeof(IReadOnlyList<>) || def == typeof(IEnumerable<>) || def == typeof(IList<>) || def == typeof(IReadOnlyCollection<>))
                return Array.CreateInstance(args[0], 0);
            if (def == typeof(IReadOnlyDictionary<,>) || def == typeof(IDictionary<,>))
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));
        }
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
