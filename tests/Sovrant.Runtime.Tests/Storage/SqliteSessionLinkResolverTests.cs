using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

/// <summary>
/// Phase 133 — sidebar labels are derived from live links, never stored. These
/// tests change a link and check the label follows, with no "type" column involved.
/// </summary>
public sealed class SqliteSessionLinkResolverTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISessionLinkResolver _resolver;
    private readonly IAgentRunStore _runs;

    public SqliteSessionLinkResolverTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _resolver = new SqliteSessionLinkResolver(_provider);
        _runs = new SqliteAgentRunStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static SessionListItem Item(string id, string? agent = null) =>
        new(id, null, DateTimeOffset.UtcNow, AgentName: agent);

    private async Task<IReadOnlyList<SessionLabel>> LabelsFor(SessionListItem item) =>
        (await _resolver.WithLabelsAsync([item])).Single().Labels!;

    private void InsertWorkflow(string id, string? sessionId, string status)
    {
        using var conn = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO workflows (id, goal, status, session_id) VALUES ($id, 'goal', $status, $sid)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$sid", (object?)sessionId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private Task Run(string kind, string? sessionId, string? member = null, string? runId = null) =>
        _runs.CreateAsync(new AgentRunRecord(
            RunId: runId ?? $"run-{Guid.NewGuid():N}", ParentRunId: null, TeamId: null, MemberId: member,
            WorkspaceId: "ws-personal-me", ProjectId: null, UserId: "me", Kind: kind, Status: "running",
            StartedAt: DateTimeOffset.UtcNow, SessionId: sessionId));

    [Fact]
    public async Task PlainChat_HasNoLabels() =>
        Assert.Empty(await LabelsFor(Item("plain")));

    [Fact]
    public async Task AttachedAgent_IsLabelled() =>
        Assert.Equal("Agent · researcher", (await LabelsFor(Item("c1", agent: "researcher"))).Single().Text);

    [Fact]
    public async Task LinkedWorkflow_ShowsItsLiveStatus()
    {
        InsertWorkflow("workflow-1", "chat-1", "running");
        var running = (await LabelsFor(Item("chat-1"))).Single();
        Assert.Equal("Workflow · Running", running.Text);
        Assert.True(running.IsActive);

        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE workflows SET status = 'awaiting_human' WHERE id = 'workflow-1'";
            cmd.ExecuteNonQuery();
        }
        var review = (await LabelsFor(Item("chat-1"))).Single();
        Assert.Equal("Workflow · Awaiting review", review.Text);
        Assert.False(review.IsActive);
    }

    [Fact]
    public async Task OlderWorkflowWithoutSessionId_UsesItsOwnIdAsTheConversation()
    {
        InsertWorkflow("workflow-legacy", null, "completed");
        Assert.Equal("Workflow · Completed", (await LabelsFor(Item("workflow-legacy"))).Single().Text);
    }

    [Fact]
    public async Task RunsLaunchedFromAChat_AreCounted()
    {
        await Run("swarm", "chat-2");
        await Run("swarm", "chat-2");
        await Run("delegation", "chat-2");
        await Run("swarm", "someone-else");

        var labels = (await LabelsFor(Item("chat-2"))).Select(l => l.Text).ToList();

        Assert.Equal(["Swarm · 2 runs", "Team · 1 run"], labels);
    }

    [Fact]
    public async Task AdHocAgentRun_IsLabelledWithItsTemplate()
    {
        await Run("adhoc-template", sessionId: null, member: "architect", runId: "run-adhoc-1");
        Assert.Equal("Agent · architect", (await LabelsFor(Item("run-adhoc-1"))).Single().Text);
    }

    [Fact]
    public async Task WebhookConversation_ShowsItsSource() =>
        Assert.Equal("Webhook · slack", (await LabelsFor(Item("webhook:slack:U123"))).Single().Text);

    [Fact]
    public async Task SeveralLinks_AllShow_InAStableOrder()
    {
        InsertWorkflow("workflow-3", "chat-3", "planning");
        await Run("swarm", "chat-3");

        var labels = await LabelsFor(Item("chat-3", agent: "writer"));

        Assert.Equal(["Agent · writer", "Workflow · Planning", "Swarm · 1 run"], labels.Select(l => l.Text));
        Assert.Equal("Agent · writer, Workflow · Planning, Swarm · 1 run", SessionLabels.Line(labels));
    }

    [Fact]
    public async Task AgentRunStore_RoundTripsSessionId()
    {
        await Run("swarm", "chat-9", runId: "run-rt");
        Assert.Equal("chat-9", (await _runs.GetAsync("run-rt"))!.SessionId);
    }

    [Fact]
    public void TurnContext_IsScopedToTheTurn()
    {
        Assert.Null(TurnContext.Current);
        using (TurnContext.Begin("chat-x", "me"))
        {
            Assert.Equal("chat-x", TurnContext.Current!.SessionId);
            using (TurnContext.Begin("chat-y", "me"))
                Assert.Equal("chat-y", TurnContext.Current!.SessionId);
            Assert.Equal("chat-x", TurnContext.Current!.SessionId);
        }
        Assert.Null(TurnContext.Current);
    }

    [Fact]
    public void SystemSessions_AreRecognised()
    {
        Assert.True(SessionLabels.IsSystemSession("__sovrant_mission_planner__"));
        Assert.False(SessionLabels.IsSystemSession("chat-1"));
    }
}
