using System.Text.Json;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Web.Adapters;

/// <summary>
/// Shows a tool-approval prompt inline in a Chat tab and waits for the answer.
/// <para>
/// Phase 145: one Web server serves many people, so a request goes only to the person whose work
/// asked for it: first a tab showing that conversation (<see cref="SessionConfig.SessionId"/>), else
/// another open tab of the same user. If they have no tab open it's denied, as before. It is never
/// shown to anyone else. The owner comes from the session (<see cref="SessionConfig.OwnerUserId"/>)
/// or, outside a conversation, the ambient principal.
/// </para>
/// </summary>
public sealed class BlazorConfirmationHandler : IToolConfirmationHandler
{
    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = [];

    /// <summary>
    /// A Chat tab listens for approvals: <paramref name="ownerUserId"/> is who's signed in on the tab,
    /// <paramref name="currentSessionId"/> the conversation it shows right now. Dispose to stop.
    /// </summary>
    public IDisposable Subscribe(string ownerUserId, Func<string?> currentSessionId, Action<ConfirmationRequest> onRequest)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerUserId);
        var sub = new Subscription(this, ownerUserId, currentSessionId, onRequest);
        lock (_gate) _subscriptions.Add(sub);
        return sub;
    }

    public async Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        var session = SessionContext.Current;
        var owner = session?.OwnerUserId ?? AmbientPrincipal.Current?.UserId;
        var target = Route(owner, session?.SessionId);
        if (target is null)
            return ConfirmationDecision.Deny;

        var request = new ConfirmationRequest(toolName, input);
        target.OnRequest(request);

        using var reg = ct.Register(() => request.Deny());
        return await request.Task.ConfigureAwait(false);
    }

    /// <summary>The tab that should see a request from <paramref name="owner"/>'s <paramref name="sessionId"/>, if any.</summary>
    internal Subscription? Route(string? owner, string? sessionId)
    {
        if (string.IsNullOrEmpty(owner))
            return null; // unknown requester: never broadcast
        lock (_gate)
        {
            var mine = _subscriptions.Where(s => string.Equals(s.Owner, owner, StringComparison.Ordinal)).ToList();
            return mine.LastOrDefault(s => sessionId is not null && string.Equals(s.CurrentSessionId(), sessionId, StringComparison.Ordinal))
                ?? mine.LastOrDefault();
        }
    }

    internal sealed class Subscription(BlazorConfirmationHandler owner, string userId, Func<string?> currentSessionId, Action<ConfirmationRequest> onRequest) : IDisposable
    {
        public string Owner { get; } = userId;
        public Func<string?> CurrentSessionId { get; } = currentSessionId;
        public Action<ConfirmationRequest> OnRequest { get; } = onRequest;

        public void Dispose()
        {
            lock (owner._gate) owner._subscriptions.Remove(this);
        }
    }
}

public sealed class ConfirmationRequest
{
    private readonly TaskCompletionSource<ConfirmationDecision> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string ToolName { get; }
    public JsonElement Input { get; }
    public Task<ConfirmationDecision> Task => _tcs.Task;

    public ConfirmationRequest(string toolName, JsonElement input)
    {
        ToolName = toolName;
        Input = input;
    }

    public void Approve() => _tcs.TrySetResult(ConfirmationDecision.AllowOnce);
    public void ApproveForTurn() => _tcs.TrySetResult(ConfirmationDecision.AllowForTurn);
    public void Deny() => _tcs.TrySetResult(ConfirmationDecision.Deny);
}
