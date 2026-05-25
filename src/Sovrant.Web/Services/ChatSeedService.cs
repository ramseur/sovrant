namespace Sovrant.Web.Services;

/// <summary>Scoped carrier for a one-shot prompt seed passed from any page to the Chat page.</summary>
public sealed class ChatSeedService
{
    private string? _pending;

    public void Set(string prompt) => _pending = prompt;

    public string? TakePrompt()
    {
        var value = _pending;
        _pending = null;
        return value;
    }
}
