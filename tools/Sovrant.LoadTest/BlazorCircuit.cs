using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sovrant.LoadTest;

/// <summary>
/// One simulated browser tab: opens a real Blazor Server circuit the way blazor.web.js does
/// (prerendered page → /_blazor hub with the "blazorpack" protocol → StartCircuit →
/// UpdateRootComponents), acknowledges every render batch, answers JS interop calls with
/// "done, no value", and can navigate between pages. It doesn't interpret render batches.
/// </summary>
public sealed partial class BlazorCircuit : IAsyncDisposable
{
    private readonly Uri _baseUri;
    private readonly CookieContainer _cookies;
    private HubConnection? _hub;
    private TaskCompletionSource _firstRender = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public long RenderBatches;
    public long JsCalls;
    public string? Error { get; private set; }

    public BlazorCircuit(Uri baseUri, CookieContainer cookies)
    {
        _baseUri = baseUri;
        _cookies = cookies;
    }

    /// <summary>Opens the circuit for <paramref name="path"/> (prerendered with <paramref name="http"/>, which carries the sign-in cookie).</summary>
    public async Task StartAsync(HttpClient http, string path, CancellationToken ct)
    {
        var pageUri = new Uri(_baseUri, path);
        var html = await http.GetStringAsync(pageUri, ct);

        var markers = MarkerRegex().Matches(html)
            .Select(m => m.Groups[1].Value)
            .Where(json => json.Contains("\"descriptor\"", StringComparison.Ordinal))
            .ToList();
        if (markers.Count == 0)
            throw new InvalidOperationException($"No interactive components on {path} — not signed in?");
        var appState = StateRegex().Match(html) is { Success: true } s ? s.Groups[1].Value : "";

        var builder = new HubConnectionBuilder()
            .WithUrl(new Uri(_baseUri, "_blazor"), o =>
            {
                o.Cookies = _cookies;
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                o.SkipNegotiation = false;
            });
        builder.Services.AddSingleton<IHubProtocol, BlazorPackProtocol>();
        if (Environment.GetEnvironmentVariable("LOADTEST_TRACE_JS") == "1")
            builder.Services.AddLogging(l => l.AddSimpleConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        _hub = builder.Build();
        _hub.ServerTimeout = TimeSpan.FromMinutes(2);

        _hub.On<long, byte[]>("JS.RenderBatch", async (batchId, _) =>
        {
            Interlocked.Increment(ref RenderBatches);
            _firstRender.TrySetResult();
            try { await _hub.SendAsync("OnRenderCompleted", batchId, null); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Error ??= ex.Message; }
        });
        // .NET 10 sends (asyncHandle, identifier, argsJson, resultType, targetInstanceId, callType).
        _hub.On("JS.BeginInvokeJS", [typeof(long), typeof(string), typeof(string), typeof(int), typeof(long), typeof(int)], async args =>
        {
            Interlocked.Increment(ref JsCalls);
            var handle = (long)args[0]!;
            if (Environment.GetEnvironmentVariable("LOADTEST_TRACE_JS") == "1")
                Console.WriteLine($"[js] {handle} {args[1]} rt={args[3]} call={args[5]}");
            if (handle == 0) return; // fire-and-forget call
            try { await _hub.SendAsync("EndInvokeJSFromDotNet", handle, true, $"[{handle},true,null]"); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Error ??= ex.Message; }
        });
        _hub.On("JS.AttachComponent", [typeof(int), typeof(string)], _ => Task.CompletedTask);
        _hub.On("JS.EndUpdateRootComponents", [typeof(long)], _ => Task.CompletedTask);
        _hub.On<string>("JS.Error", message => { Error ??= "JS.Error: " + message; _firstRender.TrySetResult(); });
        _hub.Closed += ex => { if (ex is not null) Error ??= "closed: " + ex.Message; return Task.CompletedTask; };

        await _hub.StartAsync(ct);

        var circuitId = await _hub.InvokeAsync<string?>("StartCircuit",
            _baseUri.ToString(), pageUri.ToString(), "[]", "", ct);
        if (string.IsNullOrEmpty(circuitId))
            throw new InvalidOperationException("StartCircuit was refused");

        // Same shape blazor.web.js sends: add each prerendered interactive root component.
        var operations = markers.Select((json, i) => new
        {
            type = "add",
            ssrComponentId = i + 1,
            marker = JsonDocument.Parse(json).RootElement,
        });
        var batch = JsonSerializer.Serialize(new { batchId = 1, operations });
        await _hub.SendAsync("UpdateRootComponents", batch, appState, ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await _firstRender.Task.WaitAsync(timeout.Token);
        if (Error is not null)
            throw new InvalidOperationException(Error);
    }

    /// <summary>Client-side navigation, as when someone clicks a link in the rail.</summary>
    public async Task NavigateAsync(string path, CancellationToken ct)
    {
        if (_hub is null) return;
        var before = Interlocked.Read(ref RenderBatches);
        await _hub.SendAsync("OnLocationChanged", new Uri(_baseUri, path).ToString(), null, false, ct);
        var sw = Stopwatch.StartNew();
        while (Interlocked.Read(ref RenderBatches) == before && sw.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(20, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }

    [GeneratedRegex(@"<!--Blazor:(\{.*?\})-->", RegexOptions.Singleline)]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"<!--Blazor-Server-Component-State:(.*?)-->", RegexOptions.Singleline)]
    private static partial Regex StateRegex();

    /// <summary>Blazor's hub only speaks "blazorpack", which is MessagePack under another name.</summary>
    private sealed class BlazorPackProtocol : IHubProtocol
    {
        private readonly MessagePackHubProtocol _inner = new();
        public string Name => "blazorpack";
        public int Version => _inner.Version;
        public TransferFormat TransferFormat => TransferFormat.Binary;
        public bool IsVersionSupported(int version) => _inner.IsVersionSupported(version);
        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out HubMessage? message) =>
            _inner.TryParseMessage(ref input, binder, out message);
        public void WriteMessage(HubMessage message, IBufferWriter<byte> output) => _inner.WriteMessage(message, output);
        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message) => _inner.GetMessageBytes(message);
    }
}
