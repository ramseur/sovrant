using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Lsp.JsonRpc;
using Sovrant.Lsp.Types;

namespace Sovrant.Lsp;

/// <summary>
/// Manages a single language server process, communicating via JSON-RPC 2.0 over stdio.
/// Handles the <c>initialize</c>/<c>initialized</c> handshake, document sync, and LSP requests.
/// </summary>
public sealed class LspClient : ILspClient
{
    private readonly LspServerConfig _config;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Diagnostic[]> _diagnostics = new(StringComparer.OrdinalIgnoreCase);

    private Process? _process;
    private JsonRpcTransport? _transport;
    private string _workspaceRoot = string.Empty;
    private int _documentVersion;

    public LspClient(LspServerConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Language => _config.Language;

    /// <inheritdoc/>
    public bool IsRunning => _process is not null && !_process.HasExited;

    /// <inheritdoc/>
    public async Task StartAsync(string workspaceRoot, CancellationToken ct = default)
    {
        if (IsRunning) return;

        _workspaceRoot = workspaceRoot;

        var psi = new ProcessStartInfo
        {
            FileName = _config.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspaceRoot,
        };

        foreach (var arg in _config.Args)
            psi.ArgumentList.Add(arg);

        foreach (var (key, value) in _config.Env)
            psi.Environment[key] = value;

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start language server: {_config.Command}");

        // Drain stderr to logger so it doesn't block.
        _ = Task.Run(async () =>
        {
            while (!_process.HasExited)
            {
                var line = await _process.StandardError.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is not null)
                    _logger.LogDebug("[{Language} stderr] {Line}", Language, line);
            }
        }, ct);

        _transport = new JsonRpcTransport(
            _process.StandardOutput.BaseStream,
            _process.StandardInput.BaseStream,
            _logger);

        _transport.OnNotification += HandleNotification;

        // Send initialize request.
        var initParams = new
        {
            processId = Environment.ProcessId,
            rootUri = PathToUri(workspaceRoot),
            capabilities = new
            {
                textDocument = new
                {
                    hover = new { contentFormat = new[] { "markdown", "plaintext" } },
                    definition = new { linkSupport = false },
                    references = new { },
                    rename = new { prepareSupport = false },
                    publishDiagnostics = new { relatedInformation = false },
                    synchronization = new
                    {
                        didOpen = true,
                        didChange = true,
                        willSave = false,
                        willSaveWaitUntil = false,
                        didSave = true,
                    },
                },
            },
        };

        var response = await _transport.SendRequestAsync("initialize", initParams, ct).ConfigureAwait(false);

        if (response.Error is not null)
        {
            _logger.LogError("LSP initialize failed: {Error}", response.Error.Message);
            throw new InvalidOperationException($"LSP initialize failed: {response.Error.Message}");
        }

        // Send initialized notification.
        await _transport.SendNotificationAsync("initialized", new { }, ct).ConfigureAwait(false);

        _logger.LogInformation("LSP server started for {Language}: {Command}", Language, _config.Command);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_transport is not null)
        {
            try
            {
                await _transport.SendRequestAsync("shutdown", null, ct).ConfigureAwait(false);
                await _transport.SendNotificationAsync("exit", null, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Server may already be dead.
            }

            await _transport.DisposeAsync().ConfigureAwait(false);
            _transport = null;
        }

        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            _process.Dispose();
            _process = null;
        }

        _diagnostics.Clear();
        _logger.LogInformation("LSP server stopped for {Language}", Language);
    }

    /// <inheritdoc/>
    public async Task<HoverResult?> HoverAsync(string filePath, int line, int character, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var response = await transport.SendRequestAsync("textDocument/hover", new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = PathToUri(filePath) },
            Position = new Position { Line = line, Character = character },
        }, ct).ConfigureAwait(false);

        if (response.Error is not null || response.Result is null)
            return null;

        return Deserialize<HoverResult>(response.Result.Value);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Location>> DefinitionAsync(string filePath, int line, int character, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var response = await transport.SendRequestAsync("textDocument/definition", new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = PathToUri(filePath) },
            Position = new Position { Line = line, Character = character },
        }, ct).ConfigureAwait(false);

        return ParseLocations(response);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Location>> ReferencesAsync(string filePath, int line, int character, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var response = await transport.SendRequestAsync("textDocument/references", new ReferenceParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = PathToUri(filePath) },
            Position = new Position { Line = line, Character = character },
            Context = new ReferenceContext { IncludeDeclaration = true },
        }, ct).ConfigureAwait(false);

        return ParseLocations(response);
    }

    /// <inheritdoc/>
    public IReadOnlyList<Diagnostic> GetDiagnostics(string filePath)
    {
        var uri = PathToUri(filePath);
        return _diagnostics.TryGetValue(uri, out var diags) ? diags : [];
    }

    /// <inheritdoc/>
    public async Task<WorkspaceEdit?> RenameAsync(string filePath, int line, int character, string newName, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var response = await transport.SendRequestAsync("textDocument/rename", new RenameParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = PathToUri(filePath) },
            Position = new Position { Line = line, Character = character },
            NewName = newName,
        }, ct).ConfigureAwait(false);

        if (response.Error is not null || response.Result is null)
            return null;

        return Deserialize<WorkspaceEdit>(response.Result.Value);
    }

    /// <inheritdoc/>
    public async Task NotifyDidOpenAsync(string filePath, string text, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var version = Interlocked.Increment(ref _documentVersion);
        await transport.SendNotificationAsync("textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = PathToUri(filePath),
                languageId = Language,
                version,
                text,
            },
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task NotifyDidChangeAsync(string filePath, string text, CancellationToken ct)
    {
        var transport = EnsureTransport();
        var version = Interlocked.Increment(ref _documentVersion);
        await transport.SendNotificationAsync("textDocument/didChange", new
        {
            textDocument = new { uri = PathToUri(filePath), version },
            contentChanges = new[] { new { text } }, // full sync
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private JsonRpcTransport EnsureTransport() =>
        _transport ?? throw new InvalidOperationException("LSP client is not started. Call StartAsync first.");

    private void HandleNotification(string method, JsonElement? @params)
    {
        if (method == "textDocument/publishDiagnostics" && @params.HasValue)
        {
            var diag = Deserialize<PublishDiagnosticsParams>(@params.Value);
            if (diag is not null)
                _diagnostics[diag.Uri] = diag.Diagnostics;
        }
    }

    private static Location[] ParseLocations(JsonRpcResponse response)
    {
        if (response.Error is not null || response.Result is null)
            return [];

        var result = response.Result.Value;

        // The response can be a single Location, an array of Locations, or null.
        if (result.ValueKind == JsonValueKind.Array)
            return Deserialize<Location[]>(result) ?? [];

        if (result.ValueKind == JsonValueKind.Object)
        {
            var loc = Deserialize<Location>(result);
            return loc is not null ? [loc] : [];
        }

        return [];
    }

    private static T? Deserialize<T>(JsonElement element) =>
        JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions.Default);

    public static string PathToUri(string path)
    {
        var fullPath = Path.GetFullPath(path);
        // Convert Windows backslashes to forward slashes and encode as file URI.
        var normalized = fullPath.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;
        return "file://" + normalized;
    }

    public static string UriToPath(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            return uri[8..].Replace('/', Path.DirectorySeparatorChar);
        if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return uri[7..].Replace('/', Path.DirectorySeparatorChar);
        return uri;
    }
}
