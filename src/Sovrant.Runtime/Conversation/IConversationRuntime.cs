namespace Sovrant.Runtime.Conversation;

/// <summary>Manages a stateful agentic conversation loop against an LLM provider.</summary>
public interface IConversationRuntime
{
    /// <summary>The unique identifier for this session.</summary>
    string SessionId { get; }

    /// <summary>
    /// Runs a single user turn through the agentic loop, yielding <see cref="RuntimeEvent"/>s
    /// for each step: text chunks, tool use, tool results, and the final turn completion.
    /// The loop continues automatically for tool-use turns until the model produces a final response.
    /// </summary>
    /// <param name="userMessage">The user's input text.</param>
    /// <param name="ct">A cancellation token.</param>
    IAsyncEnumerable<RuntimeEvent> RunTurnAsync(string userMessage, CancellationToken ct = default);

    /// <summary>Clears the conversation history, starting a new session.</summary>
    void Reset();
}
