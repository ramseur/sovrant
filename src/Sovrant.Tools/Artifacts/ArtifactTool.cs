using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Tools.Artifacts;

/// <summary>
/// Wraps <see cref="IArtifactStore"/> as a tool the assistant calls whenever it
/// produces a file the user might want to keep — code, documents, configs,
/// reports, data. Phase 87 Track B reframed this as the default write target
/// for any generated content (it was previously framed as agent-only). Scope
/// (workspace_id / project_id / run_id) is auto-injected by the runtime so
/// the model rarely needs to set it.
/// </summary>
public sealed class ArtifactTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Artifact", CreateSchema())
    {
        Description =
            "Save generated files (code, documents, configs, reports, data) so the user can review and keep them. " +
            "Use 'write' whenever you produce content longer than a snippet or that has a natural filename — do not paste " +
            "long file contents into chat. Actions: 'write' (save one file at a path), 'read' (retrieve a saved file by " +
            "path), 'list' (enumerate files in the current scope). Scope (workspace, project, run) is auto-attached by " +
            "the runtime — only set 'path' and 'content'.",
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
        if (string.Equals(action, "write_many", StringComparison.OrdinalIgnoreCase))
            return await WriteManyAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "read", StringComparison.OrdinalIgnoreCase))
            return await ReadAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
            return await ListAsync(input, ct).ConfigureAwait(false);

        return $"Unknown action '{action}'. Valid actions: write, write_many, read, list.";
    }

    private static ArtifactScope BuildScope(JsonElement input) => new()
    {
        WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
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

    private async Task<string> WriteManyAsync(JsonElement input, CancellationToken ct)
    {
        if (!input.TryGetProperty("entries", out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            return "Error: 'entries' (array of {path, content, content_type?}) is required for action 'write_many'.";

        if (entriesEl.GetArrayLength() == 0)
            return "Error: 'entries' must contain at least one item.";

        var scope = BuildScope(input);
        if (string.IsNullOrWhiteSpace(scope.RunId))
            return "Error: 'run_id' is required for action 'write_many'.";

        var defaultContentType = input.GetStringProp("content_type", "text/plain");

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            var written = new List<object>();
            var errors = new List<object>();

            foreach (var entry in entriesEl.EnumerateArray())
            {
                var path = entry.GetStringProp("path");
                var content = entry.GetStringProp("content");
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content))
                {
                    errors.Add(new { path, error = "missing 'path' or 'content'" });
                    continue;
                }

                var contentType = entry.GetStringProp("content_type", defaultContentType);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                try
                {
                    await _store.WriteAsync(handle, path, stream, contentType, ct).ConfigureAwait(false);
                    written.Add(new { path, size_bytes = stream.Length });
                }
                catch (ArgumentException ex)
                {
                    errors.Add(new { path, error = ex.Message });
                }
            }

            return JsonSerializer.Serialize(new
            {
                status = errors.Count == 0 ? "written" : "partial",
                run_id = scope.RunId,
                workspace_id = scope.WorkspaceId,
                project_id = scope.ProjectId,
                written_count = written.Count,
                error_count = errors.Count,
                written,
                errors,
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
                    "enum": ["write", "write_many", "read", "list"],
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
                    "description": "MIME content type (default: 'text/plain'). Optional for 'write' and 'write_many' (used as fallback for entries that omit their own content_type)."
                },
                "entries": {
                    "type": "array",
                    "description": "Files to save in one call. Required for 'write_many'. Each entry has its own path and content; share scope (run_id/workspace/project) at the top level.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "path": { "type": "string", "description": "Relative path of the file." },
                            "content": { "type": "string", "description": "File content." },
                            "content_type": { "type": "string", "description": "Optional MIME type override for this entry." }
                        },
                        "required": ["path", "content"]
                    }
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID scoping this artifact. Required for 'write' and 'read'. Optional for 'list' (omit to list across all runs)."
                },
                "workspace_id": {
                    "type": "string",
                    "description": "Workspace ID. Auto-injected by the runtime — leave unset."
                },
                "project_id": {
                    "type": "string",
                    "description": "Project ID. Auto-injected by the runtime — leave unset."
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
