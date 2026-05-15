namespace Sovrant.Runtime.TrustBoundary;

/// <summary>A detected sensitive pattern in text.</summary>
/// <param name="Start">Start index in the source text.</param>
/// <param name="Length">Length of the matched span.</param>
/// <param name="Category">Category label used for placeholder naming (e.g. "EMAIL", "SSN").</param>
/// <param name="OriginalValue">The original matched text.</param>
public sealed record Detection(int Start, int Length, string Category, string OriginalValue);

/// <summary>Detects sensitive patterns in text.</summary>
public interface IPatternDetector
{
    /// <summary>Scans <paramref name="text"/> and returns all detected sensitive spans.</summary>
    IReadOnlyList<Detection> Detect(string text);
}
