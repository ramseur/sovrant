using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Evals;

/// <summary>
/// Loads eval definitions from <c>.sovrant/evals/</c> directories.
/// Each <c>.json</c> file defines either a single eval or a suite of evals.
/// </summary>
public static partial class EvalLoader
{
    /// <summary>
    /// Loads all eval suites from the given directories (searched in order).
    /// Typically called with <c>[".sovrant/evals", "~/.sovrant/evals"]</c>.
    /// </summary>
    public static IReadOnlyList<EvalSuite> LoadAll(IReadOnlyList<string> searchPaths, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(searchPaths);

        var suites = new List<EvalSuite>();

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            foreach (var file in Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var suite = LoadFile(file);
                    if (suite is not null)
                        suites.Add(suite);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    if (logger is not null)
                        LogLoadFailed(logger, ex, file);
                }
            }
        }

        return suites;
    }

    /// <summary>Loads a single eval suite from a JSON file.</summary>
    public static EvalSuite? LoadFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = doc.RootElement;

        // Single eval definition (has "name" + "prompt" at root)
        if (root.TryGetProperty("prompt", out _) && root.TryGetProperty("name", out _))
        {
            var eval = ParseEval(root);
            return eval is null ? null : new EvalSuite
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Evals = [eval],
            };
        }

        // Suite definition (has "evals" array)
        if (root.TryGetProperty("evals", out var evalsArray) && evalsArray.ValueKind == JsonValueKind.Array)
        {
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? Path.GetFileNameWithoutExtension(filePath) : Path.GetFileNameWithoutExtension(filePath);
            var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            var tags = ParseStringArray(root, "tags");

            var evals = new List<EvalDefinition>();
            foreach (var item in evalsArray.EnumerateArray())
            {
                var eval = ParseEval(item);
                if (eval is not null)
                    evals.Add(eval);
            }

            return evals.Count > 0 ? new EvalSuite { Name = name, Description = description, Evals = evals, Tags = tags } : null;
        }

        return null;
    }

    private static EvalDefinition? ParseEval(JsonElement el)
    {
        var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
        var prompt = el.TryGetProperty("prompt", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(prompt))
            return null;

        var graderConfig = ParseGraderConfig(el);
        var category = el.TryGetProperty("category", out var c)
            ? Enum.TryParse<EvalCategory>(c.GetString(), ignoreCase: true, out var cat) ? cat : EvalCategory.Capability
            : EvalCategory.Capability;

        var attempts = el.TryGetProperty("attempts", out var a) && a.TryGetInt32(out var att) ? att : 1;
        var passThreshold = el.TryGetProperty("pass_threshold", out var pt) && pt.TryGetInt32(out var ptv) ? ptv : 1;

        return new EvalDefinition
        {
            Name = name,
            Prompt = prompt,
            Category = category,
            Grader = graderConfig,
            Attempts = Math.Max(1, attempts),
            PassThreshold = Math.Max(1, passThreshold),
            Tags = ParseStringArray(el, "tags"),
            Description = el.TryGetProperty("description", out var desc) ? desc.GetString() : null,
        };
    }

    private static GraderConfig ParseGraderConfig(JsonElement el)
    {
        // Check for explicit grader object
        if (el.TryGetProperty("grader", out var graderEl) && graderEl.ValueKind == JsonValueKind.Object)
        {
            var typeStr = graderEl.TryGetProperty("type", out var t) ? t.GetString() : "code";
            var type = typeStr?.Equals("model", StringComparison.OrdinalIgnoreCase) == true ? GraderType.Model
                : typeStr?.Equals("human", StringComparison.OrdinalIgnoreCase) == true ? GraderType.Human
                : GraderType.Code;

            return new GraderConfig
            {
                Type = type,
                Command = graderEl.TryGetProperty("command", out var cmd) ? cmd.GetString() : null,
                Pattern = graderEl.TryGetProperty("pattern", out var pat) ? pat.GetString() : null,
                Rubric = graderEl.TryGetProperty("rubric", out var rub) ? rub.GetString() : null,
                Model = graderEl.TryGetProperty("model", out var mod) ? mod.GetString() : null,
            };
        }

        // Legacy shorthand: "grader": "code" + "check": "..." at root level
        if (el.TryGetProperty("grader", out var graderStr) && graderStr.ValueKind == JsonValueKind.String)
        {
            var typeStr = graderStr.GetString();
            var type = typeStr?.Equals("model", StringComparison.OrdinalIgnoreCase) == true ? GraderType.Model
                : typeStr?.Equals("human", StringComparison.OrdinalIgnoreCase) == true ? GraderType.Human
                : GraderType.Code;

            return new GraderConfig
            {
                Type = type,
                Command = el.TryGetProperty("check", out var chk) ? chk.GetString() : null,
                Pattern = el.TryGetProperty("pattern", out var pat) ? pat.GetString() : null,
                Rubric = el.TryGetProperty("rubric", out var rub) ? rub.GetString() : null,
            };
        }

        // Default: code grader with pattern check
        return new GraderConfig
        {
            Type = GraderType.Code,
            Command = el.TryGetProperty("check", out var c) ? c.GetString() : null,
            Pattern = el.TryGetProperty("pattern", out var pa) ? pa.GetString() : null,
        };
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement el, string property) =>
        el.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? [.. arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load eval file: {File}")]
    private static partial void LogLoadFailed(ILogger logger, Exception ex, string file);
}
