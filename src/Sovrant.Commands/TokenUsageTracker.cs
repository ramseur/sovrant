namespace Sovrant.Commands;

/// <summary>
/// In-session singleton that accumulates token counts reported by
/// <see cref="Sovrant.Runtime.Conversation.RuntimeEvent.TurnComplete"/> events.
/// </summary>
public sealed class TokenUsageTracker
{
    private long _inputTokens;
    private long _outputTokens;

    /// <summary>Records tokens consumed by one turn.</summary>
    public void Record(int inputTokens, int outputTokens)
    {
        Interlocked.Add(ref _inputTokens, inputTokens);
        Interlocked.Add(ref _outputTokens, outputTokens);
    }

    /// <summary>Resets counters to zero (e.g. after /clear or /compact).</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _inputTokens, 0);
        Interlocked.Exchange(ref _outputTokens, 0);
    }

    /// <summary>Total input tokens consumed in this session.</summary>
    public long InputTokens => Interlocked.Read(ref _inputTokens);

    /// <summary>Total output tokens generated in this session.</summary>
    public long OutputTokens => Interlocked.Read(ref _outputTokens);
}
