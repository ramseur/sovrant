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
        var language = input.GetStringProp("language");
        if (string.IsNullOrWhiteSpace(language))
            return "Error: language is required.";

        var code = input.GetStringProp("code");
        if (string.IsNullOrWhiteSpace(code))
            return "Error: code is required.";

        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);

        if (!s_runtimes.TryGetValue(language, out var runtime))
        {
            var supported = string.Join(", ", s_runtimes.Keys.Distinct(StringComparer.OrdinalIgnoreCase));
            return $"Error: unsupported language '{language}'. Supported: {supported}";
        }

        try
        {
            var result = await ProcessExecutor.RunWithTempFileAsync(
                runtime.Exe, [], code, runtime.Extension, timeoutMs, ct: ct).ConfigureAwait(false);
            return result.Output;
        }
        catch (InvalidOperationException ex) { return $"Error starting {runtime.Exe}: {ex.Message}"; }
        catch (System.ComponentModel.Win32Exception ex) { return $"Error starting {runtime.Exe}: {ex.Message}"; }
    }

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
