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
    public async Task With_No_Tab_Of_Their_Own_It_Is_Denied_Not_Shown_To_Others()
    {
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        using var a = handler.Subscribe("alice@example.com", () => "s-alice", alice.Add);

        using (SessionContext.Push(Session("s-bob", "bob@example.com")))
            Assert.Equal(ConfirmationDecision.Deny, await handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken));
        // Unknown requester (no session, no signed-in user) is denied too.
        Assert.Equal(ConfirmationDecision.Deny, await handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken));
        Assert.Empty(alice);
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
    public void A_Closed_Tab_Stops_Receiving()
    {
        var handler = new BlazorConfirmationHandler();
        var alice = new List<ConfirmationRequest>();
        handler.Subscribe("alice@example.com", () => "s-alice", alice.Add).Dispose();

        using (SessionContext.Push(Session("s-alice", "alice@example.com")))
            _ = handler.RequestConfirmationAsync("Bash", Input, TestContext.Current.CancellationToken);

        Assert.Empty(alice);
    }
}
