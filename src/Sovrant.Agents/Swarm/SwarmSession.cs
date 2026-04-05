using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Records swarm events to JSONL files and replays them for status queries.
/// Sessions are stored under <c>.sovrant/swarm/sessions/{swarmId}.jsonl</c>.
/// </summary>
public sealed class SwarmSession
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _sessionsDir;

    public SwarmSession(string? sessionsDir = null)
    {
        _sessionsDir = sessionsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "swarm", "sessions");
    }

    /// <summary>Records a single event to the swarm's JSONL session file.</summary>
    public async Task RecordAsync(SwarmEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        Directory.CreateDirectory(_sessionsDir);

        var path = GetSessionPath(evt.SwarmId);
        var line = JsonSerializer.Serialize<object>(evt, s_jsonOptions);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, ct).ConfigureAwait(false);
    }

    /// <summary>Replays all events from a swarm session as an async enumerable.</summary>
    public async IAsyncEnumerable<SwarmEvent?> ReplayAsync(
        string swarmId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var path = GetSessionPath(swarmId);
        if (!File.Exists(path))
            yield break;

        using var reader = new StreamReader(path);
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            SwarmEvent? evt = null;
            try
            {
                evt = DeserializeEvent(line);
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }

            if (evt is not null)
                yield return evt;
        }
    }

    /// <summary>Lists all swarm session IDs (from file names).</summary>
    public IReadOnlyList<string> ListSessions()
    {
        if (!Directory.Exists(_sessionsDir))
            return [];

        return Directory.GetFiles(_sessionsDir, "*.jsonl")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(id => id)
            .ToList();
    }

    /// <summary>Returns whether a session file exists for the given swarm ID.</summary>
    public bool Exists(string swarmId) => File.Exists(GetSessionPath(swarmId));

    private string GetSessionPath(string swarmId) =>
        Path.Combine(_sessionsDir, $"{swarmId}.jsonl");

    private static SwarmEvent? DeserializeEvent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Discriminate by presence of unique properties
        if (root.TryGetProperty("TaskCount", out _))
            return JsonSerializer.Deserialize<SwarmEvent.PlanCreated>(json, s_jsonOptions);
        if (root.TryGetProperty("AgentName", out _))
            return JsonSerializer.Deserialize<SwarmEvent.TaskStarted>(json, s_jsonOptions);
        if (root.TryGetProperty("Output", out _))
            return JsonSerializer.Deserialize<SwarmEvent.TaskCompleted>(json, s_jsonOptions);
        if (root.TryGetProperty("Error", out _))
            return JsonSerializer.Deserialize<SwarmEvent.TaskFailed>(json, s_jsonOptions);
        if (root.TryGetProperty("FilePath", out _))
            return JsonSerializer.Deserialize<SwarmEvent.FileConflict>(json, s_jsonOptions);
        if (root.TryGetProperty("Limit", out _))
            return JsonSerializer.Deserialize<SwarmEvent.BudgetExceeded>(json, s_jsonOptions);
        if (root.TryGetProperty("FinalStatus", out _))
            return JsonSerializer.Deserialize<SwarmEvent.SwarmCompleted>(json, s_jsonOptions);
        if (root.TryGetProperty("Verdict", out _))
            return JsonSerializer.Deserialize<SwarmEvent.QualityGateCompleted>(json, s_jsonOptions);

        // QualityGateStarted has no unique properties beyond SwarmId/Timestamp
        // Check if it only has SwarmId + Timestamp (2 properties)
        if (root.EnumerateObject().Count() == 2)
            return JsonSerializer.Deserialize<SwarmEvent.QualityGateStarted>(json, s_jsonOptions);

        return null;
    }
}
