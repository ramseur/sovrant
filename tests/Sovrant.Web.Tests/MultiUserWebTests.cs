using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Web.Services;

namespace Sovrant.Web.Tests;

/// <summary>
/// Phase 145 A6 — several people on one Sovrant.Web server, end to end over HTTP. Each test client
/// has its own cookie jar, so each one is a separate browser. Runs the real Web app in-process on a
/// throwaway database.
/// </summary>
public sealed class MultiUserWebTests(WebFixture web) : IClassFixture<WebFixture>
{
    [Fact]
    public async Task Two_Browsers_Are_Two_Different_Users_At_The_Same_Time()
    {
        var alex = await web.SignedInAsync("alex@example.com");
        var sam = await web.SignedInAsync("sam@example.com");

        Assert.Equal("alex", await web.RailNameAsync(alex));
        Assert.Equal("sam", await web.RailNameAsync(sam));
        Assert.Equal("alex", await web.RailNameAsync(alex)); // unchanged by Sam signing in
    }

    [Fact]
    public async Task Signing_Out_One_Browser_Leaves_Other_Browsers_And_People_Signed_In()
    {
        var laptop = await web.SignedInAsync("jo@example.com");
        var phone = await web.SignedInAsync("jo@example.com");
        var kim = await web.SignedInAsync("kim@example.com");

        Assert.Equal(HttpStatusCode.Redirect, (await web.PostFormAsync(laptop, "/auth/logout", [])).StatusCode);
        Assert.False(await web.IsSignedInAsync(laptop));
        Assert.True(await web.IsSignedInAsync(phone));
        Assert.True(await web.IsSignedInAsync(kim));

        await web.PostFormAsync(phone, "/auth/logout", [new("scope", "all")]);
        Assert.False(await web.IsSignedInAsync(phone));
        Assert.True(await web.IsSignedInAsync(kim));
    }

    [Fact]
    public async Task An_Admin_Revoke_Sends_That_Browser_To_Sign_In_With_The_Reason()
    {
        var lee = await web.SignedInAsync("lee@example.com");
        await web.Services.GetRequiredService<IWebSignInService>().RevokeAllAsync("lee@example.com", WebSignInRevokeReasons.Admin);

        var page = await lee.GetAsync(new Uri("/dashboard", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.EndsWith("/login?reason=revoked", page.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_In_Forms_Need_The_Antiforgery_Token()
    {
        using var stranger = web.NewBrowser();
        using var form = new FormUrlEncodedContent([new("email", "alex@example.com"), new("password", WebFixture.Password)]);
        var response = await stranger.PostAsync(new Uri("/auth/login", UriKind.Relative), form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Keep_Me_Signed_In_Sets_A_Persistent_Cookie_Otherwise_It_Ends_With_The_Browser()
    {
        await web.EnsureUserAsync("pat@example.com");
        using var a = web.NewBrowser();
        var session = (await web.PostFormAsync(a, "/auth/login", [new("email", "pat@example.com"), new("password", WebFixture.Password)]))
            .Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("sovrant_session=", StringComparison.Ordinal));
        using var b = web.NewBrowser();
        var kept = (await web.PostFormAsync(b, "/auth/login", [new("email", "pat@example.com"), new("password", WebFixture.Password), new("remember", "on")]))
            .Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("sovrant_session=", StringComparison.Ordinal));

        Assert.DoesNotContain("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", kept, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sign_In_Follows_The_Admins_Settings_And_Hides_Keep_Me_Signed_In_When_Off()
    {
        // Phase 145 A7. Tests in this class share one server, so put the settings back afterwards.
        var store = web.Services.GetRequiredService<Sovrant.Runtime.Workspaces.IWorkspaceSettingsStore>();
        var source = web.Services.GetRequiredService<WebSignInPolicySource>();
        var before = WebSignInSettings.Load(store);
        await web.EnsureUserAsync("kim@example.com"); // not first run, so Sign in shows the checkbox
        try
        {
            await new WebSignInSettings(RememberAllowed: true, RememberDays: 7, IdleMinutes: 30, MaxHours: 8).SaveAsync(store);
            source.Invalidate();
            using var a = web.NewBrowser();
            var page = await a.GetStringAsync(new Uri("/login", UriKind.Relative));
            Assert.Contains("Keep me signed in on this browser for 7 days", page, StringComparison.Ordinal);
            Assert.Contains("signed out after 30 minutes without activity, or 8 hours at most", page, StringComparison.Ordinal);

            await (WebSignInSettings.Load(store) with { RememberAllowed = false }).SaveAsync(store);
            source.Invalidate();
            Assert.DoesNotContain("Keep me signed in", await a.GetStringAsync(new Uri("/login", UriKind.Relative)), StringComparison.Ordinal);

            // Ticking it anyway (an old form) doesn't keep the browser signed in.
            var cookie = (await web.PostFormAsync(a, "/auth/login", [new("email", "kim@example.com"), new("password", WebFixture.Password), new("remember", "on")]))
                .Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("sovrant_session=", StringComparison.Ordinal));
            Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await before.SaveAsync(store);
            source.Invalidate();
        }
    }

    [Fact]
    public async Task Status_Checks_Do_Not_Renew_The_Sign_In_But_Activity_Pings_Do()
    {
        var max = await web.SignedInAsync("max@example.com");
        var fiveMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-5);
        web.SetLastActive("max@example.com", fiveMinutesAgo);

        Assert.Equal(HttpStatusCode.NoContent, (await max.GetAsync(new Uri("/auth/status", UriKind.Relative))).StatusCode);
        Assert.Equal(fiveMinutesAgo.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), web.LastActive("max@example.com")[..19]);

        Assert.Equal(HttpStatusCode.NoContent, (await max.PostAsync(new Uri("/auth/ping", UriKind.Relative), null)).StatusCode);
        Assert.True(DateTimeOffset.Parse(web.LastActive("max@example.com"), System.Globalization.CultureInfo.InvariantCulture) > fiveMinutesAgo.AddMinutes(4));
    }

    [Fact]
    public async Task An_Idle_Sign_In_Is_Refused_With_Timed_Out()
    {
        var ivy = await web.SignedInAsync("ivy@example.com");
        web.SetLastActive("ivy@example.com", DateTimeOffset.UtcNow.AddHours(-2));

        var status = await ivy.GetAsync(new Uri("/auth/status", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Contains("timedout", await status.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}

/// <summary>Starts Sovrant.Web in-process on a throwaway database, with open registration and no approval.</summary>
public sealed class WebFixture : WebApplicationFactory<WebSessionService>
{
    public const string Password = "password123";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sovrant_webtests_{Guid.NewGuid():N}");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public WebFixture()
    {
        Directory.CreateDirectory(_dir);
        // Read by Program.Main before the host is built; this test assembly runs in its own process.
        Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", Path.Combine(_dir, "sovrant.db"));
        Environment.SetEnvironmentVariable("SOVRANT_LOG_FILE", "");
        Environment.SetEnvironmentVariable("SOVRANT_RUNTIME_MODE", "embedded");
    }

    public HttpClient NewBrowser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Creates the account (first one is the admin) and returns a browser signed in as it.</summary>
    public async Task<HttpClient> SignedInAsync(string email)
    {
        await EnsureUserAsync(email);
        var browser = NewBrowser();
        var response = await PostFormAsync(browser, "/auth/login", [new("email", email), new("password", Password)]);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
        return browser;
    }

    public async Task EnsureUserAsync(string email)
    {
        await _gate.WaitAsync();
        try
        {
            var identity = Services.GetRequiredService<IIdentityService>();
            if (!_ready)
            {
                if (await identity.IsFirstRunAsync())
                    await identity.RegisterAsync("owner@example.com", Password, issueToken: false);
                await identity.SetRegistrationOpenAsync(true);
                await identity.SetApprovalRequiredAsync(false);
                _ready = true;
            }
            var users = Services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
            if (await users.GetAsync(email) is null)
                Assert.True((await identity.RegisterAsync(email, Password, issueToken: false)).Success);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Posts a form the way the page does: with the antiforgery token from /login.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string url, List<KeyValuePair<string, string>> fields)
    {
        // Signed-out browsers get the token from Sign in; signed-in ones are redirected away from it,
        // so they take it from Settings (which carries the sign-out form).
        var page = await browser.GetAsync(new Uri("/login", UriKind.Relative));
        if (page.StatusCode != HttpStatusCode.OK)
            page = await browser.GetAsync(new Uri("/settings", UriKind.Relative));
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "no antiforgery token found");
        fields.Add(new("__RequestVerificationToken", token));
        using var form = new FormUrlEncodedContent(fields);
        return await browser.PostAsync(new Uri(url, UriKind.Relative), form);
    }

    public async Task<bool> IsSignedInAsync(HttpClient browser) =>
        (await browser.GetAsync(new Uri("/auth/status", UriKind.Relative))).StatusCode == HttpStatusCode.NoContent;

    /// <summary>The name in the rail footer of the prerendered Home page: who this browser is.</summary>
    public async Task<string?> RailNameAsync(HttpClient browser)
    {
        var html = await browser.GetStringAsync(new Uri("/dashboard", UriKind.Relative));
        var m = Regex.Match(html, "class=\"rail-footer-name\">([^<]*)<");
        return m.Success ? m.Groups[1].Value : null;
    }

    public void SetLastActive(string userId, DateTimeOffset when) =>
        Sql("UPDATE web_sign_ins SET last_active_at = $t WHERE user_id = $u AND revoked_at IS NULL",
            ("$t", when.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)), ("$u", userId));

    public string LastActive(string userId)
    {
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_active_at FROM web_sign_ins WHERE user_id = $u AND revoked_at IS NULL ORDER BY created_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$u", userId);
        return (string)cmd.ExecuteScalar()!;
    }

    private Microsoft.Data.Sqlite.SqliteConnection OpenDb()
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_dir, "sovrant.db")}");
        conn.Open();
        return conn;
    }

    private void Sql(string sql, params (string Name, string Value)[] args)
    {
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        cmd.ExecuteNonQuery();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }
}
