using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;

namespace Sovrant.Runtime.Tests.Auth;

/// <summary>
/// Phase 145 — per-browser Web sign-ins: 1 hour idle (renewed by activity), 12 hours at most,
/// 14 days with "Keep me signed in" (A7; was 30); revoke one or all; limits set by admins (A7) or env.
/// Real SQLite, fake clock.
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
    public async Task Keep_Me_Signed_In_Lasts_14_Days_Without_An_Idle_Limit()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, Policy);
        Assert.True(signIn.Remember);

        _clock.Advance(TimeSpan.FromDays(13));
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
    public async Task An_Open_Tab_Can_Check_Its_Sign_In_Without_Counting_As_Activity()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: false, null, null, Policy);

        _clock.Advance(TimeSpan.FromMinutes(50));
        Assert.True((await _signIns.CheckByIdAsync(signIn.SignInId, Policy)).IsValid); // a status check, not activity
        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckByIdAsync(signIn.SignInId, Policy)).Status);
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, Policy, touch: true)).Status);
        Assert.Equal(WebSignInStatus.Unknown, (await _signIns.CheckByIdAsync("wsi-nope", Policy)).Status);
    }

    [Fact]
    public async Task Admins_See_Everyones_Active_Sign_Ins_In_One_List()
    {
        await _signIns.CreateAsync("sam@example.com", false, "Chrome on Windows", null, Policy);
        var (gone, _) = await _signIns.CreateAsync("sam@example.com", false, "Firefox on Linux", null, Policy);
        await _signIns.CreateAsync("admin@example.com", true, "Edge on Windows", null, Policy);
        await _signIns.RevokeAsync(gone.SignInId, WebSignInRevokeReasons.SignOut);

        var all = await _signIns.ListAllActiveAsync(Policy);
        Assert.Equal(2, all.Count);
        Assert.DoesNotContain(all, s => s.SignInId == gone.SignInId);
        Assert.Equal("Chrome on Windows", WebSignInText.DescribeBrowser("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36"));
        Assert.Equal("Safari on iPhone", WebSignInText.DescribeBrowser("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1"));
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

    // ── Phase 145 A7: admins set the rules; tightening them reaches existing sign-ins ──────────

    [Fact]
    public async Task A_Shorter_Keep_Signed_In_Length_Shortens_Existing_Remembered_Sign_Ins()
    {
        var (_, token) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, Policy); // 14 days
        var sevenDays = Policy with { RememberLifetime = TimeSpan.FromDays(7) };

        _clock.Advance(TimeSpan.FromDays(6));
        Assert.True((await _signIns.CheckAsync(token, sevenDays, touch: false)).IsValid);
        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, sevenDays, touch: false)).Status);
    }

    [Fact]
    public async Task Turning_Keep_Me_Signed_In_Off_Makes_Remembered_Sign_Ins_Follow_The_Idle_Limit()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, Policy);
        var off = Policy with { RememberLifetime = TimeSpan.Zero };
        Assert.False(off.KeepsSignedIn(signIn));

        _clock.Advance(TimeSpan.FromMinutes(59));
        Assert.True((await _signIns.CheckAsync(token, off, touch: false)).IsValid);
        _clock.Advance(TimeSpan.FromMinutes(2)); // 61 min idle
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, off, touch: false)).Status);
    }

    [Fact]
    public async Task A_Longer_Limit_Never_Extends_An_Existing_Sign_In()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: true, null, null, Policy); // 14 days
        var ninety = Policy with { RememberLifetime = TimeSpan.FromDays(90) };
        Assert.Equal(signIn.ExpiresAt, ninety.ExpiryOf(signIn));

        _clock.Advance(TimeSpan.FromDays(14));
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, ninety, touch: false)).Status);
    }

    [Fact]
    public async Task A_Shorter_Maximum_Shortens_Existing_Sign_Ins()
    {
        var (signIn, token) = await _signIns.CreateAsync("sam@example.com", remember: false, null, null, Policy); // 12 h
        var eight = Policy with { AbsoluteLifetime = TimeSpan.FromHours(8) };
        Assert.Equal(signIn.CreatedAt + TimeSpan.FromHours(8), eight.ExpiryOf(signIn));

        for (var i = 0; i < 16; i++) // active every 30 minutes for 8 hours
        {
            _clock.Advance(TimeSpan.FromMinutes(30));
            if (i < 15) Assert.True((await _signIns.CheckAsync(token, eight, touch: true)).IsValid);
        }
        Assert.Equal(WebSignInStatus.TimedOut, (await _signIns.CheckAsync(token, eight, touch: true)).Status);
    }

    [Fact]
    public async Task Admin_Settings_Win_Over_Env_Variables_Which_Win_Over_Defaults()
    {
        var store = new MemorySettings();
        var env = new Dictionary<string, string?> { [WebSignInPolicy.IdleMinutesVariable] = "30", [WebSignInPolicy.RememberDaysVariable] = "0" };
        string? Env(string k) => env.GetValueOrDefault(k);

        var fromEnv = WebSignInSettings.Load(store, Env);
        Assert.Equal(30, fromEnv.IdleMinutes);   // env starting value
        Assert.False(fromEnv.RememberAllowed);   // 0 days = off
        Assert.Equal(14, fromEnv.RememberDays);  // default length kept for when it's turned on
        Assert.Equal(12, fromEnv.MaxHours);      // default

        await new WebSignInSettings(RememberAllowed: true, RememberDays: 7, IdleMinutes: 120, MaxHours: 8).SaveAsync(store);
        var saved = WebSignInSettings.Load(store, Env);
        Assert.Equal(new WebSignInSettings(true, 7, 120, 8), saved); // the admin's choice wins
        Assert.Equal(new WebSignInPolicy(TimeSpan.FromHours(2), TimeSpan.FromHours(8), TimeSpan.FromDays(7)), saved.ToPolicy());

        Assert.Equal(new WebSignInSettings(true, 14, 60, 12), WebSignInSettings.Load(null, _ => null)); // defaults
    }

    [Fact]
    public async Task The_Policy_Source_Picks_Up_A_Saved_Change_After_Invalidate()
    {
        var store = new MemorySettings();
        var source = new WebSignInPolicySource(store, _ => null);
        Assert.True(source.Current.RememberAllowed);

        await (WebSignInSettings.Load(store, _ => null) with { RememberAllowed = false }).SaveAsync(store);
        source.Invalidate();
        Assert.False(source.Current.RememberAllowed);
    }

    [Theory]
    [InlineData(60, "1 hour")]
    [InlineData(15, "15 minutes")]
    [InlineData(480, "8 hours")]
    [InlineData(20160, "14 days")]
    [InlineData(1440, "1 day")]
    public void Durations_Read_Naturally(int minutes, string expected) =>
        Assert.Equal(expected, WebSignInSettings.Describe(TimeSpan.FromMinutes(minutes)));

    private sealed class MemorySettings : Sovrant.Runtime.Workspaces.IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) => GetGlobalAsync(key, ct);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) { _data[key] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) { _data.Remove(key); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_data, StringComparer.Ordinal));
    }

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
