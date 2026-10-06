using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;

namespace Sovrant.Runtime.Tests.Auth;

/// <summary>
/// Phase 145 — per-browser Web sign-ins: 1 hour idle (renewed by activity), 12 hours at most,
/// 30 days with "Keep me signed in"; revoke one or all; env-settable limits. Real SQLite, fake clock.
/// </summary>
public sealed class WebSignInServiceTests : IAsyncDisposable
{
    private readonly string _baseDir;
    private readonly SqliteStorageProvider _storage;
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero));
    private readonly SqliteWebSignInService _signIns;
    private static readonly WebSignInPolicy Policy = WebSignInPolicy.Default;

    public WebSignInServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"sovrant_signin_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, Path.Combine(_baseDir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();
        var users = new SqliteUserStore(_storage, NullLogger<SqliteUserStore>.Instance);
        users.CreateAsync(userId: "sam@example.com").GetAwaiter().GetResult();
        users.CreateAsync(userId: "admin@example.com", role: "admin").GetAwaiter().GetResult();
        _signIns = new SqliteWebSignInService(_storage, _clock);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_baseDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task A_New_Sign_In_Is_Valid_And_Carries_The_User_And_Role()
    {
        var (signIn, token) = await _signIns.CreateAsync("admin@example.com", remember: false, "Edge on Windows", "127.0.0.1", Policy);

        var check = await _signIns.CheckAsync(token, Policy, touch: true);
        Assert.True(check.IsValid);
        Assert.Equal("admin@example.com", check.SignIn!.UserId);
        Assert.Equal("admin", check.Role);
        Assert.Equal(signIn.CreatedAt + TimeSpan.FromHours(12), signIn.ExpiresAt);
        Assert.Equal(WebSignInStatus.Unknown, (await _signIns.CheckAsync("sws_not-a-real-token", Policy, touch: false)).Status);
    }

    [Fact]
    public async Task Idle_For_An_Hour_Signs_You_Out_But_Activity_Renews_It()
    {
        var (_, token) = await _signIns.CreateAsync("sam@example.com", remember: false, null, null, Policy);

        _clock.Advance(TimeSpan.FromMinutes(50));
        Assert.True((await _signIns.CheckAsync(token, Policy, touch: true)).IsValid); // activity at 50 min

        _clock.Advance(TimeSpan.FromMinutes(50));                                     // 100 min total, 50 since activity
        Assert.True((await _signIns.CheckAsync(token, Policy, touch: false)).IsValid);

        _clock.Advance(TimeSpan.FromMinutes(11));                                     // 61 min since activity
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, Policy, touch: true)).Status);
    }

    [Fact]
    public async Task Twelve_Hours_Is_The_Limit_However_Active_You_Are()
    {
        var (_, token) = await _signIns.CreateAsync("sam@example.com", remember: false, null, null, Policy);
        for (var i = 0; i < 23; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(30));
            Assert.True((await _signIns.CheckAsync(token, Policy, touch: true)).IsValid);
        }
        _clock.Advance(TimeSpan.FromMinutes(30)); // 12 hours
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, Policy, touch: true)).Status);
    }

    [Fact]
    public async Task Keep_Me_Signed_In_Lasts_30_Days_Without_An_Idle_Limit()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, Policy);
        Assert.True(signIn.Remember);

        _clock.Advance(TimeSpan.FromDays(29));
        Assert.True((await _signIns.CheckAsync(token, Policy, touch: false)).IsValid);
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, Policy, touch: false)).Status);
    }

    [Fact]
    public async Task Remember_Is_Ignored_When_The_Admin_Turned_It_Off()
    {
        var noRemember = Policy with { RememberLifetime = TimeSpan.Zero };
        var (signIn, _) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, noRemember);
        Assert.False(signIn.Remember);
        Assert.Equal(signIn.CreatedAt + TimeSpan.FromHours(12), signIn.ExpiresAt);
    }

    [Fact]
    public async Task Revoke_One_Or_Everywhere()
    {
        var (laptop, laptopToken) = await _signIns.CreateAsync("sam@example.com", false, "Chrome on Windows", "10.0.4.21", Policy);
        var (_, phoneToken) = await _signIns.CreateAsync("sam@example.com", true, "Safari on iPhone", "198.51.100.7", Policy);
        Assert.Equal(2, (await _signIns.ListActiveAsync("sam@example.com", Policy)).Count);

        Assert.True(await _signIns.RevokeAsync(laptop.SignInId, WebSignInRevokeReasons.Admin));
        var laptopCheck = await _signIns.CheckAsync(laptopToken, Policy, touch: true);
        Assert.Equal(WebSignInStatus.Revoked, laptopCheck.Status);
        Assert.Equal(WebSignInRevokeReasons.Admin, laptopCheck.RevokedReason);
        Assert.Single(await _signIns.ListActiveAsync("sam@example.com", Policy));

        Assert.Equal(1, await _signIns.RevokeAllAsync("sam@example.com", WebSignInRevokeReasons.SignOutEverywhere));
        Assert.Equal(WebSignInStatus.Revoked, (await _signIns.CheckAsync(phoneToken, Policy, touch: false)).Status);
        Assert.Empty(await _signIns.ListActiveAsync("sam@example.com", Policy));
    }

    [Fact]
    public void Limits_Come_From_The_Environment()
    {
        var env = new Dictionary<string, string>
        {
            [WebSignInPolicy.IdleMinutesVariable] = "30",
            [WebSignInPolicy.MaxSessionHoursVariable] = "8",
            [WebSignInPolicy.RememberDaysVariable] = "0",
        };
        var policy = WebSignInPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.IdleTimeout);
        Assert.Equal(TimeSpan.FromHours(8), policy.AbsoluteLifetime);
        Assert.False(policy.RememberAllowed);

        var defaults = WebSignInPolicy.FromEnvironment(k => k == WebSignInPolicy.IdleMinutesVariable ? "nonsense" : null);
        Assert.Equal(WebSignInPolicy.Default, defaults);
    }

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
