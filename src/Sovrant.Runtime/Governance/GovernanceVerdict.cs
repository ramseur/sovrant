namespace Sovrant.Runtime.Governance;

/// <summary>The action to take based on governance evaluation.</summary>
public enum GovernanceAction
{
    /// <summary>Allow the operation to proceed.</summary>
    Allow,

    /// <summary>Allow but include a warning in the output.</summary>
    Warn,

    /// <summary>Block the operation entirely.</summary>
    Block,
}

/// <summary>The result of evaluating an operation against governance rules.</summary>
/// <param name="Action">Whether to allow, warn, or block.</param>
/// <param name="Reason">Human-readable explanation of the verdict.</param>
/// <param name="Rule">The rule that triggered this verdict (e.g. "SecretDetection", "DangerousCommand").</param>
public sealed record GovernanceVerdict(GovernanceAction Action, string Reason, string Rule = "")
{
    /// <summary>A pre-built verdict for allowed operations.</summary>
    public static GovernanceVerdict Allowed { get; } = new(GovernanceAction.Allow, string.Empty);
}
