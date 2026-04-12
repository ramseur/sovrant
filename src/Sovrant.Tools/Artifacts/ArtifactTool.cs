using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Tools.Artifacts;

/// <summary>
/// Phase 41 — agent-callable artifact tools that wrap <see cref="IArtifactStore"/>.
/// Agents work in isolation and deposit deliverables (code, reports, data) into
/// the artifact store scoped to workspace/project/run. The team leader or
/// orchestrator reads results and decides what happens next. This is NOT an
/// inter-agent messaging channel — agents produce artifacts, the orchestrator
/// and users consume them.
/// </summary>
public sealed class ArtifactTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Artifact", CreateSchema())
    {
        Description =
            "Deposit or retrieve run-scoped work products. Actions: 'write' (store a deliverable — code, report, data), " +
            "'read' (retrieve an artifact by path), 'list' (list artifacts in a scope). " +
            "Agents produce artifacts in isolation; the orchestrator or user consumes results. " +
            "Do NOT use this for agent-to-agent messaging — deposit your output and return.",
    };

    private readonly IArtifactStore _store;

    public ArtifactTool(IArtifactStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var action = input.GetStringProp("action");

        if (string.Equals(action, "write", StringComparison.OrdinalIgnoreCase))
            return await WriteAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "read", StringComparison.OrdinalIgnoreCase))
            return await ReadAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
            return await ListAsync(input, ct).ConfigureAwait(false);

        return $"Unknown action '{action}'. Valid actions: write, read, list.";
    }

    private static ArtifactScope BuildScope(JsonElement input) => new()
    {
        WorkspaceId = input.GetStringProp("workspace_id", ArtifactScope.DefaultWorkspaceId),
        ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
        RunId = input.GetStringProp("run_id"),
    };

    private async Task<string> WriteAsync(JsonElement input, CancellationToken ct)
    {
        var path = input.GetStringProp("path");
        if (string.IsNullOrWhiteSpace(path))
            return "Error: 'path' is required for action 'write'.";

        var content = input.GetStringProp("content");
        if (string.IsNullOrWhiteSpace(content))
            return "Error: 'content' is required for action 'write'.";

        var scope = BuildScope(input);
        if (string.IsNullOrWhiteSpace(scope.RunId))
            return "Error: 'run_id' is required for action 'write'.";

        var contentType = input.GetStringProp("content_type", "text/plain");

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await _store.WriteAsync(handle, path, stream, contentType, ct).ConfigureAwait(false);

            return JsonSerializer.Serialize(new
            {
                status = "written",
                path,
                run_id = scope.RunId,
                workspace_id = scope.WorkspaceId,
                project_id = scope.ProjectId,
                size_bytes = stream.Length,
            });
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> ReadAsync(JsonElement input, CancellationToken ct)
    {
        var path = input.GetStringProp("path");
        if (string.IsNullOrWhiteSpace(path))
            return "Error: 'path' is required for action 'read'.";

        var scope = BuildScope(input);
        if (string.IsNullOrWhiteSpace(scope.RunId))
            return "Error: 'run_id' is required for action 'read'.";

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            using var stream = await _store.ReadAsync(handle, path, ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

            // Cap content returned to agent to prevent prompt overload — the whole
            // point of artifacts is to avoid dumping huge content into prompts.
            const int maxChars = 100_000;
            var truncated = content.Length > maxChars;
            if (truncated)
                content = content[..maxChars];

            return JsonSerializer.Serialize(new
            {
                path,
                content,
                truncated,
                size_bytes = content.Length,
            });
        }
        catch (FileNotFoundException)
        {
            return $"Error: artifact '{path}' not found in run '{scope.RunId}'.";
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> ListAsync(JsonElement input, CancellationToken ct)
    {
        var scope = BuildScope(input);

        var entries = new List<object>();
        var limit = input.GetIntProp("limit", 100);
        var count = 0;

        await foreach (var entry in _store.ListAsync(scope, ct).ConfigureAwait(false))
        {
            if (++count > limit) break;
            entries.Add(new
            {
                path = entry.RelativePath,
                size_bytes = entry.SizeBytes,
                content_type = entry.ContentType,
                last_modified = entry.LastModified,
                run_id = entry.RunId,
            });
        }

        return JsonSerializer.Serialize(new
        {
            workspace_id = scope.WorkspaceId,
            project_id = scope.ProjectId,
            run_id = scope.RunId,
            count = entries.Count,
            truncated = count > limit,
            entries,
        });
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["write", "read", "list"],
                    "description": "The artifact action to perform."
                },
                "path": {
                    "type": "string",
                    "description": "Relative path of the artifact (e.g. 'report.md', 'output/data.json'). Required for 'write' and 'read'."
                },
                "content": {
                    "type": "string",
                    "description": "The artifact content to store (required for 'write')."
                },
                "content_type": {
                    "type": "string",
                    "description": "MIME content type (default: 'text/plain'). Optional for 'write'."
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID scoping this artifact. Required for 'write' and 'read'. Optional for 'list' (omit to list across all runs)."
                },
                "workspace_id": {
                    "type": "string",
                    "description": "Workspace ID (defaults to 'personal')."
                },
                "project_id": {
                    "type": "string",
                    "description": "Project ID (defaults to 'default-project')."
                },
                "limit": {
                    "type": "integer",
                    "description": "Max results for 'list' (default 100)."
                }
            },
            "required": ["action"]
        }
        """).RootElement;
}
