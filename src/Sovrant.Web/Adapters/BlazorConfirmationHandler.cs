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
/// another open tab of the same user. With none open it waits for them (A8.2). It is never
/// shown to anyone else. The owner comes from the session (<see cref="SessionConfig.OwnerUserId"/>)
/// or, outside a conversation, the ambient principal.
/// </para>
/// </summary>
public sealed class BlazorConfirmationHandler : IToolConfirmationHandler
{
    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<ConfirmationRequest> _pending = [];

    /// <summary>Raised (with the owner's user id) when that person's waiting approvals change.</summary>
    public event Action<string>? PendingChanged;

    public IDisposable Subscribe(string ownerUserId, Func<string?> currentSessionId, Action<ConfirmationRequest> onRequest)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerUserId);
        var sub = new Subscription(this, ownerUserId, currentSessionId, onRequest);
        lock (_gate) _subscriptions.Add(sub);
        Deliver(ownerUserId); // anything that waited while none of their tabs was open
        return sub;
    }

    /// <summary>
    /// Phase 145 A8.2: waits for the person to answer. With none of their tabs open the request waits —
    /// until they open one, the run is stopped, or it reaches the run time limit — instead of being refused.
    /// </summary>
    public async Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        var session = SessionContext.Current;
        var owner = session?.OwnerUserId ?? AmbientPrincipal.Current?.UserId;
        if (string.IsNullOrEmpty(owner))
            return ConfirmationDecision.Deny; // unknown requester: never broadcast

        var request = new ConfirmationRequest(toolName, input) { OwnerUserId = owner, SessionId = session?.SessionId };
        lock (_gate) _pending.Add(request);
        Deliver(owner);
        PendingChanged?.Invoke(owner);
        try
        {
            using var reg = ct.Register(() => request.Deny());
            return await request.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate) _pending.Remove(request);
            PendingChanged?.Invoke(owner);
        }
    }

    /// <summary>This person's approvals that are waiting for an answer.</summary>
    public IReadOnlyList<ConfirmationRequest> PendingFor(string? ownerUserId)
    {
        if (string.IsNullOrEmpty(ownerUserId)) return [];
        lock (_gate) return _pending.Where(r => r.OwnerUserId == ownerUserId && !r.Task.IsCompleted).ToList();
    }

    /// <summary>Hands each undelivered request of <paramref name="owner"/> to their best open tab.</summary>
    private void Deliver(string owner)
    {
        List<(Subscription Sub, ConfirmationRequest Request)> sends = [];
        lock (_gate)
        {
            foreach (var r in _pending.Where(r => r.OwnerUserId == owner && r.DeliveredTo is null && !r.Task.IsCompleted))
            {
                if (RouteLocked(owner, r.SessionId) is not { } target) continue;
                r.DeliveredTo = target;
                sends.Add((target, r));
            }
        }
        foreach (var (sub, r) in sends) sub.OnRequest(r);
    }

    internal Subscription? Route(string? owner, string? sessionId)
    {
        if (string.IsNullOrEmpty(owner)) return null;
        lock (_gate) return RouteLocked(owner, sessionId);
    }

    private Subscription? RouteLocked(string owner, string? sessionId)
    {
        var mine = _subscriptions.Where(s => string.Equals(s.Owner, owner, StringComparison.Ordinal)).ToList();
        return mine.LastOrDefault(s => sessionId is not null && string.Equals(s.CurrentSessionId(), sessionId, StringComparison.Ordinal))
            ?? mine.LastOrDefault();
    }

    internal sealed class Subscription(BlazorConfirmationHandler owner, string userId, Func<string?> currentSessionId, Action<ConfirmationRequest> onRequest) : IDisposable
    {
        public string Owner { get; } = userId;
        public Func<string?> CurrentSessionId { get; } = currentSessionId;
        public Action<ConfirmationRequest> OnRequest { get; } = onRequest;

        public void Dispose()
        {
            lock (owner._gate)
            {
                owner._subscriptions.Remove(this);
                // Requests this tab was showing go back to waiting, for another tab or the next one opened.
                foreach (var r in owner._pending.Where(r => ReferenceEquals(r.DeliveredTo, this)))
                    r.DeliveredTo = null;
            }
            owner.Deliver(Owner);
        }
    }
}

public sealed class ConfirmationRequest
{
    private readonly TaskCompletionSource<ConfirmationDecision> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string ToolName { get; }
    public JsonElement Input { get; }
    /// <summary>Whose run is asking, and in which conversation (Phase 145 A8.2).</summary>
    public string? OwnerUserId { get; init; }
    public string? SessionId { get; init; }
    internal BlazorConfirmationHandler.Subscription? DeliveredTo { get; set; }
    public bool IsAnswered => Task.IsCompleted;
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
