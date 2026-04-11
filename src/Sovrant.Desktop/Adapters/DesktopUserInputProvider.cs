using System.Threading;
using System.Threading.Tasks;
using Sovrant.Tools.Extended;

namespace Sovrant.Desktop.Adapters;

/// <summary>
/// Desktop input provider that returns the question as inline text rather than
/// showing a modal popup. The LLM will surface the question in the chat stream
/// and the user can reply with their next message.
/// </summary>
public sealed class DesktopUserInputProvider : IUserInputProvider
{
    public Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        // Return a marker so the LLM knows the user hasn't answered yet.
        // The question text will already appear in the chat via the tool use block.
        return Task.FromResult($"[Please reply in the chat to answer: {question}]");
    }
}
