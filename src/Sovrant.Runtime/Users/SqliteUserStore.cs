using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Users;

/// <summary>
/// SQLite-backed implementation of <see cref="IUserService"/> over the Phase 32
/// <c>users</c> table.
///
/// <para><b>Security posture (Phase 37):</b></para>
/// <list type="bullet">
///   <item>All SQL is parameterized — no identifier or value interpolation.</item>
///   <item>Inputs are validated at the boundary (slug regex, length caps,
///         enum whitelists). Validation failures throw <see cref="ArgumentException"/>.</item>
///   <item>Server generates the <c>user_id</c> as <c>usr_{16hex}</c>; callers may
///         not pass arbitrary IDs through the public API surface.</item>
///   <item>Hard-delete is intentionally not exposed. <see cref="DeactivateAsync"/>
///         flips <c>status='inactive'</c>, preserving every FK reference and audit row.</item>
///   <item>Mutations are logged via <see cref="ILogger"/> (no PII beyond user_id/username).</item>
/// </list>
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
    Justification = "Database stores enum values as lowercase by convention.")]
internal sealed partial class SqliteUserStore : IUserService
{
    // Slug-like — no whitespace, no shell metacharacters, no path separators.
    [GeneratedRegex(@"^[a-zA-Z0-9._-]{1,64}$")]
    private static partial Regex UsernameRegex();

    // Defensive email shape check — full RFC validation is out of scope.
    [GeneratedRegex(@"^[^@\s]{1,64}@[^@\s]{1,189}\.[^@\s]{1,64}$")]
    private static partial Regex EmailRegex();

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "user", "admin",
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "active", "inactive", "pending",
    };

    private const int MaxTeamLength = 64;
    private const int MaxEmailLength = 254;

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteUserStore> _logger;

    public SqliteUserStore(ISqliteConnectionFactory connectionFactory, ILogger<SqliteUserStore> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [LoggerMessage(EventId = 3700, Level = LogLevel.Information, Message = "User created: {UserId} username={Username}")]
    private static partial void LogUserCreated(ILogger logger, string userId, string username);

    [LoggerMessage(EventId = 3701, Level = LogLevel.Information, Message = "User updated: {UserId}")]
    private static partial void LogUserUpdated(ILogger logger, string userId);

    [LoggerMessage(EventId = 3702, Level = LogLevel.Information, Message = "User deactivated: {UserId}")]
    private static partial void LogUserDeactivated(ILogger logger, string userId);

    [LoggerMessage(EventId = 3703, Level = LogLevel.Information, Message = "User reactivated: {UserId}")]
    private static partial void LogUserReactivated(ILogger logger, string userId);

    // ── CRUD ───────────────────────────────────────────────────────────────

    public async Task<User> CreateAsync(
        string username,
        string? email = null,
        string role = "user",
        string? team = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        ValidateUsername(username);
        ValidateEmail(email);
        ValidateRole(role);
        ValidateTeam(team);

        // Server-generated ID by default. If a caller passes one (e.g. the
        // SeedDefaultUser path that uses the OS username), validate it as
        // a slug to prevent injection of arbitrary text into the PK.
        var resolvedUserId = string.IsNullOrEmpty(userId)
            ? GenerateUserId()
            : ValidateAndReturn(userId);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = resolvedUserId,
            Username = username,
            Email = string.IsNullOrEmpty(email) ? null : email,
            Role = role,
            Team = string.IsNullOrEmpty(team) ? null : team,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now,
        };

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (user_id, username, email, role, team, status, created_at, updated_at)
            VALUES ($id, $username, $email, $role, $team, $status, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", user.UserId);
        cmd.Parameters.AddWithValue("$username", user.Username);
        cmd.Parameters.AddWithValue("$email", (object?)user.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$role", user.Role);
        cmd.Parameters.AddWithValue("$team", (object?)user.Team ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", user.Status);
        cmd.Parameters.AddWithValue("$created", Ts(user.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", Ts(user.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        LogUserCreated(_logger, user.UserId, user.Username);
        return user;
    }

    public async Task<User?> GetAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, username, email, role, team, status, created_at, updated_at
            FROM users WHERE user_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", userId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadUser(reader) : null;
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(username)) return null;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, username, email, role, team, status, created_at, updated_at
            FROM users WHERE username = $username
            """;
        cmd.Parameters.AddWithValue("$username", username);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadUser(reader) : null;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) return null;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, username, email, role, team, status, created_at, updated_at
            FROM users WHERE email = $email
            """;
        cmd.Parameters.AddWithValue("$email", email);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadUser(reader) : null;
    }

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await GetAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return null;

        using var connection = _connectionFactory.CreateConnection();
        using var statsCmd = connection.CreateCommand();
        statsCmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM sessions WHERE user_id = $uid),
                (SELECT COALESCE(SUM(input_tokens), 0) FROM token_usage WHERE user_id = $uid),
                (SELECT COALESCE(SUM(output_tokens), 0) FROM token_usage WHERE user_id = $uid),
                (SELECT COALESCE(SUM(cost_usd), 0) FROM token_usage WHERE user_id = $uid),
                (SELECT MAX(updated_at) FROM sessions WHERE user_id = $uid)
            """;
        statsCmd.Parameters.AddWithValue("$uid", userId);

        using var reader = await statsCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new UserProfile
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Team = user.Team,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            };
        }

        var sessionCount = (int)reader.GetInt64(0);
        var inputTokens = reader.GetInt64(1);
        var outputTokens = reader.GetInt64(2);
        var costUsd = reader.GetDouble(3);
        var lastSeen = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false)
            ? (DateTimeOffset?)null
            : ParseTs(reader.GetString(4));

        return new UserProfile
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            Team = user.Team,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            SessionCount = sessionCount,
            TotalInputTokens = inputTokens,
            TotalOutputTokens = outputTokens,
            TotalCostUsd = costUsd,
            LastSeenAt = lastSeen,
        };
    }

    public async Task<IReadOnlyList<User>> ListAsync(UserListFilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new UserListFilter();
        ValidateFilter(filter);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = """
            SELECT user_id, username, email, role, team, status, created_at, updated_at
            FROM users
            WHERE 1=1
            """;
        if (!string.IsNullOrEmpty(filter.Status))
        {
            sql += " AND status = $status";
            cmd.Parameters.AddWithValue("$status", filter.Status);
        }
        if (!string.IsNullOrEmpty(filter.Role))
        {
            sql += " AND role = $role";
            cmd.Parameters.AddWithValue("$role", filter.Role);
        }
        if (!string.IsNullOrEmpty(filter.Team))
        {
            sql += " AND team = $team";
            cmd.Parameters.AddWithValue("$team", filter.Team);
        }
        sql += " ORDER BY created_at DESC LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$limit", filter.Limit);
        cmd.Parameters.AddWithValue("$offset", filter.Offset);
        cmd.CommandText = sql;

        var results = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(ReadUser(reader));
        return results;
    }

    public async Task<int> CountAsync(UserListFilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new UserListFilter();
        ValidateFilter(filter);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = "SELECT COUNT(*) FROM users WHERE 1=1";
        if (!string.IsNullOrEmpty(filter.Status))
        {
            sql += " AND status = $status";
            cmd.Parameters.AddWithValue("$status", filter.Status);
        }
        if (!string.IsNullOrEmpty(filter.Role))
        {
            sql += " AND role = $role";
            cmd.Parameters.AddWithValue("$role", filter.Role);
        }
        if (!string.IsNullOrEmpty(filter.Team))
        {
            sql += " AND team = $team";
            cmd.Parameters.AddWithValue("$team", filter.Team);
        }
        cmd.CommandText = sql;

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task<User?> UpdateAsync(
        string userId,
        string? username = null,
        string? email = null,
        string? role = null,
        string? team = null,
        string? status = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("user_id is required.", nameof(userId));

        // Validate only the fields that are being updated.
        if (username is not null) ValidateUsername(username);
        if (email is not null) ValidateEmail(email);
        if (role is not null) ValidateRole(role);
        if (team is not null) ValidateTeam(team);
        if (status is not null) ValidateStatus(status);

        // Build the SET clause from the non-null fields. All identifiers are
        // string literals — never user-supplied — so this is injection-safe.
        var setClauses = new List<string>();
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        if (username is not null)
        {
            setClauses.Add("username = $username");
            cmd.Parameters.AddWithValue("$username", username);
        }
        if (email is not null)
        {
            setClauses.Add("email = $email");
            cmd.Parameters.AddWithValue("$email", email.Length == 0 ? DBNull.Value : (object)email);
        }
        if (role is not null)
        {
            setClauses.Add("role = $role");
            cmd.Parameters.AddWithValue("$role", role);
        }
        if (team is not null)
        {
            setClauses.Add("team = $team");
            cmd.Parameters.AddWithValue("$team", team.Length == 0 ? DBNull.Value : (object)team);
        }
        if (status is not null)
        {
            setClauses.Add("status = $status");
            cmd.Parameters.AddWithValue("$status", status);
        }

        if (setClauses.Count == 0)
            return await GetAsync(userId, ct).ConfigureAwait(false);

        setClauses.Add("updated_at = $updated");
        cmd.Parameters.AddWithValue("$updated", Ts(DateTimeOffset.UtcNow));
        cmd.Parameters.AddWithValue("$id", userId);

        // CA2100: setClauses are constructed exclusively from string literals
        // above (never user input). User-supplied values flow through parameters.
#pragma warning disable CA2100
        cmd.CommandText = $"UPDATE users SET {string.Join(", ", setClauses)} WHERE user_id = $id";
#pragma warning restore CA2100
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (rows == 0) return null;

        LogUserUpdated(_logger, userId);
        return await GetAsync(userId, ct).ConfigureAwait(false);
    }

    public async Task<bool> DeactivateAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return false;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE users
            SET status = 'inactive', updated_at = $updated
            WHERE user_id = $id AND status != 'inactive'
            """;
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.Parameters.AddWithValue("$updated", Ts(DateTimeOffset.UtcNow));
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (rows > 0)
            LogUserDeactivated(_logger, userId);
        return rows > 0;
    }

    public async Task<bool> ReactivateAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return false;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE users
            SET status = 'active', updated_at = $updated
            WHERE user_id = $id AND status = 'inactive'
            """;
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.Parameters.AddWithValue("$updated", Ts(DateTimeOffset.UtcNow));
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (rows > 0)
            LogUserReactivated(_logger, userId);
        return rows > 0;
    }

    // ── Per-user data views ────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ListSessionsAsync(string userId, int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return Array.Empty<string>();
        if (limit <= 0 || limit > 1000) limit = 100;
        if (offset < 0) offset = 0;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id FROM sessions
            WHERE user_id = $uid
            ORDER BY started_at DESC
            LIMIT $limit OFFSET $offset
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        var results = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(reader.GetString(0));
        return results;
    }

    public async Task<UserUsage> GetUsageAsync(
        string userId,
        string? model = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Aggregate totals.
        using var totalsCmd = connection.CreateCommand();
        var totalsSql = """
            SELECT
                COALESCE(SUM(input_tokens), 0),
                COALESCE(SUM(output_tokens), 0),
                COALESCE(SUM(cost_usd), 0),
                COUNT(DISTINCT session_id)
            FROM token_usage
            WHERE user_id = $uid
            """;
        totalsCmd.Parameters.AddWithValue("$uid", userId);
        if (!string.IsNullOrEmpty(model))
        {
            totalsSql += " AND model = $model";
            totalsCmd.Parameters.AddWithValue("$model", model);
        }
        if (from.HasValue)
        {
            totalsSql += " AND recorded_at >= $from";
            totalsCmd.Parameters.AddWithValue("$from", Ts(from.Value));
        }
        if (to.HasValue)
        {
            totalsSql += " AND recorded_at <= $to";
            totalsCmd.Parameters.AddWithValue("$to", Ts(to.Value));
        }
        totalsCmd.CommandText = totalsSql;

        long totalInput = 0, totalOutput = 0;
        double totalCost = 0;
        int sessionCount = 0;
        using (var reader = await totalsCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                totalInput = reader.GetInt64(0);
                totalOutput = reader.GetInt64(1);
                totalCost = reader.GetDouble(2);
                sessionCount = (int)reader.GetInt64(3);
            }
        }

        // Per-model breakdown.
        using var byModelCmd = connection.CreateCommand();
        var byModelSql = """
            SELECT model, SUM(input_tokens), SUM(output_tokens), COALESCE(SUM(cost_usd), 0)
            FROM token_usage
            WHERE user_id = $uid
            """;
        byModelCmd.Parameters.AddWithValue("$uid", userId);
        if (!string.IsNullOrEmpty(model))
        {
            byModelSql += " AND model = $model";
            byModelCmd.Parameters.AddWithValue("$model", model);
        }
        if (from.HasValue)
        {
            byModelSql += " AND recorded_at >= $from";
            byModelCmd.Parameters.AddWithValue("$from", Ts(from.Value));
        }
        if (to.HasValue)
        {
            byModelSql += " AND recorded_at <= $to";
            byModelCmd.Parameters.AddWithValue("$to", Ts(to.Value));
        }
        byModelSql += " GROUP BY model ORDER BY SUM(input_tokens) + SUM(output_tokens) DESC";
        byModelCmd.CommandText = byModelSql;

        var byModel = new List<UserUsageByModel>();
        using (var reader = await byModelCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                byModel.Add(new UserUsageByModel
                {
                    Model = reader.GetString(0),
                    InputTokens = reader.GetInt64(1),
                    OutputTokens = reader.GetInt64(2),
                    CostUsd = reader.GetDouble(3),
                });
            }
        }

        return new UserUsage
        {
            UserId = userId,
            TotalInputTokens = totalInput,
            TotalOutputTokens = totalOutput,
            TotalCostUsd = totalCost,
            SessionCount = sessionCount,
            ByModel = byModel,
        };
    }

    public async Task<IReadOnlyList<UserAuditEvent>> ListAuditEventsAsync(string userId, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return Array.Empty<UserAuditEvent>();
        if (limit <= 0 || limit > 1000) limit = 100;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        // audit_governance has no user_id; join through sessions.
        cmd.CommandText = """
            SELECT a.id, a.timestamp, a.session_id, a.workspace_id, a.project_id,
                   a.phase, a.tool, a.action, a.rule, a.reason
            FROM audit_governance a
            INNER JOIN sessions s ON s.session_id = a.session_id
            WHERE s.user_id = $uid
            ORDER BY a.id DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<UserAuditEvent>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new UserAuditEvent
            {
                Id = reader.GetInt64(0),
                Timestamp = ParseTs(reader.GetString(1)),
                SessionId = await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2),
                WorkspaceId = await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetString(3),
                ProjectId = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4),
                Phase = reader.GetString(5),
                Tool = reader.GetString(6),
                Action = reader.GetString(7),
                Rule = reader.GetString(8),
                Reason = reader.GetString(9),
            });
        }
        return results;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string GenerateUserId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return $"usr_{Convert.ToHexStringLower(bytes)}";
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("username is required.", nameof(username));
        if (!UsernameRegex().IsMatch(username))
            throw new ArgumentException(
                "username must match ^[a-zA-Z0-9._-]{1,64}$",
                nameof(username));
    }

    private static string ValidateAndReturn(string userId)
    {
        ValidateUsername(userId); // same slug rules apply to user_id
        return userId;
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return;
        if (email.Length > MaxEmailLength)
            throw new ArgumentException($"email must be ≤ {MaxEmailLength} characters.", nameof(email));
        if (!EmailRegex().IsMatch(email))
            throw new ArgumentException("email is not a valid email address.", nameof(email));
    }

    private static void ValidateRole(string role)
    {
        if (string.IsNullOrEmpty(role) || !AllowedRoles.Contains(role))
            throw new ArgumentException(
                $"role must be one of: {string.Join(", ", AllowedRoles)}",
                nameof(role));
    }

    private static void ValidateStatus(string status)
    {
        if (string.IsNullOrEmpty(status) || !AllowedStatuses.Contains(status))
            throw new ArgumentException(
                $"status must be one of: {string.Join(", ", AllowedStatuses)}",
                nameof(status));
    }

    private static void ValidateTeam(string? team)
    {
        if (team is null) return;
        if (team.Length > MaxTeamLength)
            throw new ArgumentException($"team must be ≤ {MaxTeamLength} characters.", nameof(team));
    }

    private static void ValidateFilter(UserListFilter filter)
    {
        if (filter.Limit <= 0 || filter.Limit > 1000)
            throw new ArgumentException("limit must be between 1 and 1000.", nameof(filter));
        if (filter.Offset < 0)
            throw new ArgumentException("offset must be non-negative.", nameof(filter));
        if (filter.Status is not null && !AllowedStatuses.Contains(filter.Status))
            throw new ArgumentException(
                $"status filter must be one of: {string.Join(", ", AllowedStatuses)}",
                nameof(filter));
        if (filter.Role is not null && !AllowedRoles.Contains(filter.Role))
            throw new ArgumentException(
                $"role filter must be one of: {string.Join(", ", AllowedRoles)}",
                nameof(filter));
        if (filter.Team is not null && filter.Team.Length > MaxTeamLength)
            throw new ArgumentException($"team filter must be ≤ {MaxTeamLength} characters.", nameof(filter));
    }

    private static User ReadUser(SqliteDataReader reader) => new()
    {
        UserId = reader.GetString(0),
        Username = reader.GetString(1),
        Email = reader.IsDBNull(2) ? null : reader.GetString(2),
        Role = reader.GetString(3),
        Team = reader.IsDBNull(4) ? null : reader.GetString(4),
        Status = reader.GetString(5),
        CreatedAt = ParseTs(reader.GetString(6)),
        UpdatedAt = ParseTs(reader.GetString(7)),
    };

    private static string Ts(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string s) =>
        DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
}
