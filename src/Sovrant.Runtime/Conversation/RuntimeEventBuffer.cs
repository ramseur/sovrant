namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Keeps a running turn's events so a chat you navigated away from can be rebuilt when you come back.
/// Text arrives as many small <see cref="RuntimeEvent.TextChunk"/>s, so consecutive chunks are merged
/// into one: the buffer stays small however long the reply is. (It used to stop at 500 events, so
/// coming back to a long reply lost its middle.) Other events are kept as they are, up to a generous
/// limit that only a runaway turn would reach.
/// </summary>
public static class RuntimeEventBuffer
{
    public const int MaxEvents = 5000;

    /// <summary>Adds <paramref name="evt"/> to <paramref name="buffer"/> (call under the caller's lock).</summary>
    public static void Add(IList<object> buffer, object evt)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (evt is RuntimeEvent.TextChunk chunk && buffer.Count > 0 && buffer[^1] is RuntimeEvent.TextChunk previous)
        {
            buffer[^1] = new RuntimeEvent.TextChunk(previous.Text + chunk.Text);
            return;
        }
        if (buffer.Count < MaxEvents)
            buffer.Add(evt);
    }
}
