namespace Sovrant.Tools.Quality;

/// <summary>The discrete phases of the verification pipeline.</summary>
public enum VerificationPhase
{
    Build,
    TypeCheck,
    Lint,
    Test,
    SecurityScan,
    DiffReview,
}
