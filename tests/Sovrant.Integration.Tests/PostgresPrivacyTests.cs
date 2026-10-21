using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Storage.Postgres;
using Xunit;

namespace Sovrant.Integration.Tests;

/// <summary>
/// Phase 134 — conversation privacy on the Postgres backend, against a real database.
/// Before the fix, Postgres lists never read is_private / workspace_id (every row looked public, so
/// admins saw private titles in Command Center), reading privacy threw (INTEGER cast to bool) and
/// setting it failed (boolean into an INTEGER column).
/// Skipped unless SOVRANT_TEST_PG holds an Npgsql connection string, e.g.
/// <c>Host=localhost;Port=55432;Username=postgres;Password=sovrant;Database=sovrant_test</c>.
/// </summary>
public sealed class PostgresPrivacyTests
{
    [PostgresFact]
    public async Task New_Conversations_Are_Private_And_Lists_Report_Privacy_And_Workspace()
    {
        var cs = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
        using var sp = new ServiceCollection().AddSovrantPostgresStorage(cs).BuildServiceProvider();
        await sp.GetRequiredService<ISchemaInitializer>().InitializeAsync();
        var store = sp.GetRequiredService<ISessionStore>();

        var owner = $"pgtest-{Guid.NewGuid():N}@example.com";
        var sid = $"pgtest-{Guid.NewGuid():N}";
        try
        {
            await store.AppendAsync(sid, new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "hello"), ownerUserId: owner);
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("UPDATE sessions SET workspace_id = 'ws-team' WHERE session_id = $1", conn);
                cmd.Parameters.AddWithValue(sid);
                await cmd.ExecuteNonQueryAsync();
            }

            // Created private, readable without throwing.
            Assert.True(await store.GetIsPrivateAsync(sid));
            var row = Assert.Single(await store.ListWithTitlesAsync(owner));
            Assert.True(row.IsPrivate);
            Assert.Equal("ws-team", row.WorkspaceId);
            Assert.Equal(owner, row.OwnerUserId);
            Assert.Contains(await store.ListWithTitlesAsync(), r => r.SessionId == sid && r.IsPrivate); // the all-rows list Command Center uses

            // Making it Public works and shows up in both lists.
            await store.UpdatePrivacyAsync(sid, owner, isPrivate: false);
            Assert.False(await store.GetIsPrivateAsync(sid));
            Assert.False(Assert.Single(await store.ListWithTitlesAsync(owner)).IsPrivate);
        }
        finally
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            foreach (var table in new[] { "session_entries", "sessions" })
            {
                await using var cmd = new NpgsqlCommand($"DELETE FROM {table} WHERE session_id = $1", conn);
                cmd.Parameters.AddWithValue(sid);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

/// <summary>Runs only when <c>SOVRANT_TEST_PG</c> holds a Postgres connection string.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "SOVRANT_TEST_PG";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a Postgres connection string to run Postgres tests.";
    }
}
