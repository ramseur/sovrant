using Sovrant.Tools.Extended;

namespace Sovrant.Web.Adapters;

public sealed class BlazorUserInputProvider : IUserInputProvider
{
    public Task<string> AskAsync(string question, CancellationToken ct = default)
        => Task.FromResult($"[Please reply in the chat to answer: {question}]");
}
