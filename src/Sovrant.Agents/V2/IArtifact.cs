namespace Sovrant.Agents.V2;

/// <summary>
/// [V2 Placeholder — not yet implemented. See Phase 19 roadmap.]
/// <para>
/// A versioned piece of content produced or consumed by an agent during a
/// multi-agent collaboration: source files, test reports, architectural plans,
/// review notes, generated schemas, etc.
/// </para>
/// </summary>
public interface IArtifact
{
    /// <summary>Unique identifier for this artifact within its <see cref="ITeamWorkspace"/>.</summary>
    string Id { get; }

    /// <summary>Human-readable name, e.g. <c>"AuthService.cs"</c> or <c>"test_report.json"</c>.</summary>
    string Name { get; }

    /// <summary>MIME type of the artifact content, e.g. <c>"text/plain"</c> or <c>"application/json"</c>.</summary>
    string ContentType { get; }

    /// <summary>Name of the agent that produced this artifact.</summary>
    string ProducedByAgent { get; }

    /// <summary>UTC timestamp when this artifact was created.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>Reads the artifact content as a UTF-8 string.</summary>
    Task<string> ReadAsync(CancellationToken ct = default);
}
