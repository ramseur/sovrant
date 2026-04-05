using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Executes code in a language-specific subprocess (Python, Node.js, etc.).</summary>
public sealed class ReplTool : ITool
{
    private const int DefaultTimeoutMs = 30_000;

    // Resolve python executable once: prefer python3 on Unix, python on Windows.
    private static readonly string s_pythonExe =
        OperatingSystem.IsWindows() ? "python" : "python3";

    private static readonly IReadOnlyDictionary<string, (string Exe, string Extension)> s_runtimes =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = (s_pythonExe, ".py"),
            ["python3"] = (s_pythonExe, ".py"),
            ["javascript"] = ("node", ".js"),
            ["js"] = ("node", ".js"),
            ["typescript"] = ("ts-node", ".ts"),
            ["ts"] = ("ts-node", ".ts"),
            ["bash"] = ("bash", ".sh"),
            ["sh"] = ("sh", ".sh"),
            ["ruby"] = ("ruby", ".rb"),
            ["perl"] = ("perl", ".pl"),
        };

    private static readonly ToolDefinition s_definition = new("REPL", CreateSchema())
    {
        Description =
            "Executes code in a subprocess for the specified language. " +
            "Supported languages: python, javascript, bash, ruby, perl. " +
            "Returns combined stdout and stderr.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var language = GetString(input, "language");
        if (string.IsNullOrWhiteSpace(language))
            return "Error: language is required.";

        var code = GetString(input, "code");
        if (string.IsNullOrWhiteSpace(code))
            return "Error: code is required.";

        var timeoutMs = GetInt(input, "timeout", DefaultTimeoutMs);

        if (!s_runtimes.TryGetValue(language, out var runtime))
        {
            var supported = string.Join(", ", s_runtimes.Keys.Distinct(StringComparer.OrdinalIgnoreCase));
            return $"Error: unsupported language '{language}'. Supported: {supported}";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        // Write code to a temp file to avoid shell injection via argument escaping.
        var scriptFile = Path.Combine(Path.GetTempPath(), $"sovrant_repl_{Guid.NewGuid():N}{runtime.Extension}");
        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        try
        {
            await File.WriteAllTextAsync(scriptFile, code, cts.Token).ConfigureAwait(false);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = runtime.Exe,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add(scriptFile);

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutSb.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrSb.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var sb = new StringBuilder();
            if (stdoutSb.Length > 0) sb.Append(stdoutSb);
            if (stderrSb.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"[stderr]\n{stderrSb}");
            if (process.ExitCode != 0) sb.Append(CultureInfo.InvariantCulture, $"\n[exit code: {process.ExitCode}]");

            return sb.Length > 0 ? sb.ToString() : string.Create(CultureInfo.InvariantCulture, $"[exit code: {process.ExitCode}]");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"Error: execution timed out after {timeoutMs} ms.";
        }
        catch (InvalidOperationException ex) { return $"Error starting {runtime.Exe}: {ex.Message}"; }
        catch (System.ComponentModel.Win32Exception ex) { return $"Error starting {runtime.Exe}: {ex.Message}"; }
        finally
        {
            try { File.Delete(scriptFile); } catch (IOException) { /* best-effort cleanup */ }
        }
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "language": {"type": "string",  "description": "Programming language (python, javascript, bash, etc.)."},
                "code":     {"type": "string",  "description": "Code to execute."},
                "timeout":  {"type": "integer", "description": "Timeout in milliseconds (default 30000)."}
            },
            "required": ["language", "code"]
        }
        """).RootElement;
}
