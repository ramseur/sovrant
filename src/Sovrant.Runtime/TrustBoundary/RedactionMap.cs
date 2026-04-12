using System.Globalization;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>
/// Bidirectional mapping of original sensitive values to deterministic placeholders.
/// Scoped to a single request/response pair — never persisted, never sent to a provider.
/// </summary>
public sealed class RedactionMap
{
    private readonly Dictionary<string, string> _originalToPlaceholder = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _placeholderToOriginal = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _categoryCounters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of redacted values in this map.</summary>
    public int Count => _originalToPlaceholder.Count;

    /// <summary>True if no redactions were recorded.</summary>
    public bool IsEmpty => _originalToPlaceholder.Count == 0;

    /// <summary>
    /// Gets or creates a placeholder for the given original value.
    /// Same original value always maps to the same placeholder within this map.
    /// </summary>
    public string GetOrCreatePlaceholder(string originalValue, string category)
    {
        if (_originalToPlaceholder.TryGetValue(originalValue, out var existing))
            return existing;

        if (!_categoryCounters.TryGetValue(category, out var counter))
            counter = 0;

        counter++;
        _categoryCounters[category] = counter;

        var placeholder = string.Create(CultureInfo.InvariantCulture, $"[{category}_{counter}]");
        _originalToPlaceholder[originalValue] = placeholder;
        _placeholderToOriginal[placeholder] = originalValue;
        return placeholder;
    }

    /// <summary>Replaces all placeholders in <paramref name="text"/> with original values.</summary>
    public string Restore(string text)
    {
        if (string.IsNullOrEmpty(text) || _placeholderToOriginal.Count == 0)
            return text;

        var result = text;
        foreach (var (placeholder, original) in _placeholderToOriginal)
        {
            result = result.Replace(placeholder, original, StringComparison.Ordinal);
        }
        return result;
    }

    /// <summary>Returns all redaction entries for audit logging.</summary>
    public IReadOnlyDictionary<string, string> Redactions => _originalToPlaceholder;
}
