using System.Text.Json;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Web.Adapters;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 145 — on a shared Web server a tool-approval prompt goes only to the person whose work asked
/// for it (preferring the tab showing that conversation), never to anyone else.
/// </summary>
public sealed class WebApprovalRoutingTests
{
    private static readonly JsonElement Input = JsonDocument.Parse("{}").RootElement;

    private static SessionConfig Session(string id, string owner) => new() { SessionId = id, OwnerUserId = owner };

    [Fact]
    public async Task A_Request_Reaches_Only_Its_Owner()
    {
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        var bob = new List<ConfirmationRequest>();
        using var a = handler.Subscribe("alice@example.com", () => "s-alice", alice.Add);
        using var b = handler.Subscribe("bob@example.com", () => "s-bob", bob.Add);

        Task<ConfirmationDecision> pending;
        using (SessionContext.Push(Session("s-alice", "alice@example.com")))
            pending = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        var request = Assert.Single(alice);
        Assert.Empty(bob);
        request.Approve();
        Assert.Equal(ConfirmationDecision.AllowOnce, await pending);
    }

    [Fact]
    public void The_Tab_Showing_The_Conversation_Wins()
    {
        var handler = new BlazorConfirmationHandler();
        var tabA = new List<ConfirmationRequest>();
        var tabB = new List<ConfirmationRequest>();
        using var a = handler.Subscribe("alice@example.com", () => "s-1", tabA.Add);
        using var b = handler.Subscribe("alice@example.com", () => "s-2", tabB.Add);

        using (SessionContext.Push(Session("s-1", "alice@example.com")))
            _ = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        Assert.Single(tabA);
        Assert.Empty(tabB);
    }

    [Fact]
    public async Task With_No_Tab_Of_Their_Own_It_Waits_And_Is_Never_Shown_To_Others()
    {
        // Phase 145 A8.2: it used to be refused at once; now it waits for its owner.
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        using var a = handler.Subscribe("alice@example.com", () => "s-alice", alice.Add);

        Task<ConfirmationDecision> pending;
        using (SessionContext.Push(Session("s-bob", "bob@example.com")))
            pending = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);                       // waiting, not refused
        Assert.Empty(alice);                                     // never shown to someone else
        Assert.Single(handler.PendingFor("bob@example.com"));

        // Unknown requester (no session, no signed-in user) is still refused.
        Assert.Equal(ConfirmationDecision.Deny, await handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_Waiting_Request_Reaches_The_Owner_When_They_Open_A_Tab()
    {
        var handler = new BlazorConfirmationHandler();
        Task<ConfirmationDecision> pending;
        using (SessionContext.Push(Session("s-bob", "bob@example.com")))
            pending = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        var bob = new List<ConfirmationRequest>();
        using var b = handler.Subscribe("bob@example.com", () => "s-bob", bob.Add); // Bob signs back in
        var request = Assert.Single(bob);
        Assert.Equal("s-bob", request.SessionId);
        request.Approve();

        Assert.Equal(ConfirmationDecision.AllowOnce, await pending);
        Assert.Empty(handler.PendingFor("bob@example.com"));
    }

    [Fact]
    public async Task A_Request_On_A_Tab_That_Closes_Moves_To_Another_Tab()
    {
        var handler = new BlazorConfirmationHandler();
        var first = new List<ConfirmationRequest>();
        var tab1 = handler.Subscribe("bob@example.com", () => "s-bob", first.Add);
        Task<ConfirmationDecision> pending;
        using (SessionContext.Push(Session("s-bob", "bob@example.com")))
            pending = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);
        Assert.Single(first);

        tab1.Dispose();                                          // closed without answering
        var second = new List<ConfirmationRequest>();
        using var tab2 = handler.Subscribe("bob@example.com", () => "s-bob", second.Add);
        Assert.Same(first[0], Assert.Single(second));
        second[0].Deny();
        Assert.Equal(ConfirmationDecision.Deny, await pending);
    }

    [Fact]
    public async Task Stopping_The_Run_Ends_The_Wait()
    {
        var handler = new BlazorConfirmationHandler();
        using var cts = new CancellationTokenSource();
        Task<ConfirmationDecision> pending;
        using (SessionContext.Push(Session("s-bob", "bob@example.com")))
            pending = handler.RequestConfirmationAsync("Bash", Input, cts.Token);

        await cts.CancelAsync(); // run stopped, or the run time limit reached
        Assert.Equal(ConfirmationDecision.Deny, await pending);
        Assert.Empty(handler.PendingFor("bob@example.com"));
    }

    [Fact]
    public void Work_Outside_A_Conversation_Goes_To_The_Signed_In_Users_Tab()
    {
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        var bob = new List<ConfirmationRequest>();
        using var a = handler.Subscribe("alice@example.com", () => "s-alice", alice.Add);
        using var b = handler.Subscribe("bob@example.com", () => "s-bob", bob.Add);

        using (AmbientPrincipal.Push("bob@example.com", "user"))
            _ = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        Assert.Single(bob);
        Assert.Empty(alice);
    }

    [Fact]
    public void A_Closed_Tab_Stops_Receiving_The_Request_Waits_Instead()
    {
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        handler.Subscribe("alice@example.com", () => "s-alice", alice.Add).Dispose();

        using (SessionContext.Push(Session("s-alice", "alice@example.com")))
            _ = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        Assert.Empty(alice);
        Assert.Single(handler.PendingFor("alice@example.com"));
    }
}
