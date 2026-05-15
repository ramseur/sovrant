using System.Text.Json;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// Extracts a <see cref="SessionSummary"/> from a list of <see cref="SessionEntry"/> records
/// loaded from a session's JSONL transcript.
/// </summary>
public static class SessionSummaryExtractor
{
    /// <summary>
    /// Analyses session entries and produces a condensed summary.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="project">The project directory the session was associated with.</param>
    /// <param name="entries">The JSONL entries for the session.</param>
    public static SessionSummary Extract(string sessionId, string project, IReadOnlyList<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return new SessionSummary
            {
                SessionId = sessionId,
                Project = project,
                StartedAt = DateTimeOffset.UtcNow,
                EndedAt = DateTimeOffset.UtcNow,
                Outcome = SessionOutcome.Unknown,
            };
        }

        var tasks = new List<string>();
        var toolsUsed = new HashSet<string>(StringComparer.Ordinal);
        var filesModified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var turnCount = 0;
        var errorCount = 0;
        var lastTurnHadError = false;

        foreach (var entry in entries)
        {
            totalInputTokens += entry.InputTokens;
            totalOutputTokens += entry.OutputTokens;

            switch (entry.Role)
            {
                case "user":
                    turnCount++;
                    lastTurnHadError = false;
                    // Extract the first line of user messages as task descriptions
                    var firstLine = ExtractFirstLine(entry.Content);
                    if (!string.IsNullOrWhiteSpace(firstLine))
                        tasks.Add(firstLine);
                    break;

                case "assistant":
                    // Track tool names from assistant entries
                    if (entry.ToolName is not null)
                        toolsUsed.Add(entry.ToolName);
                    break;

                case "tool_result":
                    if (entry.ToolName is not null)
                        toolsUsed.Add(entry.ToolName);

                    if (entry.IsError)
                    {
                        errorCount++;
                        lastTurnHadError = true;
                    }

                    // Extract file paths from tool results for write/edit tools
                    ExtractFilePaths(entry, filesModified);
                    break;
            }
        }

        var outcome = entries.Count < 2
            ? SessionOutcome.Unknown
            : lastTurnHadError ? SessionOutcome.Failure : SessionOutcome.Success;

        return new SessionSummary
        {
            SessionId = sessionId,
            Project = project,
            StartedAt = entries[0].Timestamp,
            EndedAt = entries[^1].Timestamp,
            Tasks = tasks,
            ToolsUsed = toolsUsed.Order(StringComparer.Ordinal).ToList(),
            FilesModified = filesModified.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            Outcome = outcome,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens,
            TurnCount = turnCount,
            ErrorCount = errorCount,
        };
    }

    private static string ExtractFirstLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var span = content.AsSpan().TrimStart();
        var newline = span.IndexOfAny('\r', '\n');
        var line = newline >= 0 ? span[..newline] : span;

        // Cap at 200 chars to keep summaries concise
        return line.Length > 200
            ? new string(line[..200]) + "..."
            : new string(line);
    }

    private static readonly HashSet<string> s_fileTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file", "edit_file", "str_replace", "WriteFile", "EditFile",
    };

    private static void ExtractFilePaths(SessionEntry entry, HashSet<string> filesModified)
    {
        if (entry.ToolName is null || !s_fileTools.Contains(entry.ToolName))
            return;

        // Try to parse the content as JSON to extract file paths
        try
        {
            using var doc = JsonDocument.Parse(entry.Content);
            foreach (var prop in new[] { "path", "file_path", "filepath" })
            {
                if (doc.RootElement.TryGetProperty(prop, out var val) &&
                    val.ValueKind == JsonValueKind.String)
                {
                    var path = val.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                        filesModified.Add(path);
                    return;
                }
            }
        }
        catch (JsonException)
        {
            // Content is not JSON — skip
        }
    }
}
