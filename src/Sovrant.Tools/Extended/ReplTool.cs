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

    private static readonly IReadOnlyDictionary<string, (string Exe, string Args)> s_runtimes =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = ("python3", "-c \"{code}\""),
            ["python3"] = ("python3", "-c \"{code}\""),
            ["javascript"] = ("node", "-e \"{code}\""),
            ["js"] = ("node", "-e \"{code}\""),
            ["typescript"] = ("ts-node", "-e \"{code}\""),
            ["ts"] = ("ts-node", "-e \"{code}\""),
            ["bash"] = ("bash", "-c \"{code}\""),
            ["sh"] = ("sh", "-c \"{code}\""),
            ["ruby"] = ("ruby", "-e \"{code}\""),
            ["perl"] = ("perl", "-e \"{code}\""),
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

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        try
        {
            var args = runtime.Args.Replace("{code}", code.Replace("\"", "\\\"", StringComparison.Ordinal),
                StringComparison.Ordinal);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = runtime.Exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

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
