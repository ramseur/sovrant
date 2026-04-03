using Sovrant.Api.Types;

namespace Sovrant.Api.Providers;

/// <summary>Abstraction over a single LLM provider endpoint.</summary>
public interface ILlmProvider
{
    /// <summary>The provider's unique name (e.g. "openai-compat", "ollama", "provider-api").</summary>
    string Name { get; }

    /// <summary>The base URL of this provider's API.</summary>
    Uri BaseUrl { get; }

    /// <summary>Sends a non-streaming messages request and returns the full response.</summary>
    /// <param name="req">The messages request.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default);

    /// <summary>Sends a streaming messages request and yields SSE events as they arrive.</summary>
    /// <param name="req">The messages request.</param>
    /// <param name="ct">A cancellation token.</param>
    IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, CancellationToken ct = default);
}
