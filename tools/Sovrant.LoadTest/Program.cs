using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.LoadTest;
using Sovrant.Runtime;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;

// Phase 145 Part E — load test for embedded Sovrant.Web. See README.md next to this file.
//
//   dotnet run -c Release --project tools/Sovrant.LoadTest -- --users 300
//
// Starts its own Web server on a throwaway database (unless --url is given), creates the accounts,
// signs each one in through the real sign-in form, opens one Blazor circuit per user, optionally
// clicks between pages, and measures the server process's memory before, during and after.

var opt = Options.Parse(args);
var inv = CultureInfo.InvariantCulture;
var log = new StringBuilder();
void Say(string line) { Console.WriteLine(line); log.AppendLine(line); }

var dataDir = Path.Combine(Path.GetTempPath(), $"sovrant_load_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
Process? web = null;
var baseUri = opt.Url ?? new Uri($"http://127.0.0.1:{opt.Port}/");

try
{
    if (opt.Url is null)
    {
        Directory.CreateDirectory(dataDir);
        web = StartWeb(opt, dataDir);
        Say($"Started Sovrant.Web (pid {web.Id}) on {baseUri} - data in {dataDir}");
    }
    await WaitReadyAsync(baseUri);

    if (opt.Url is null)
    {
        var sw = Stopwatch.StartNew();
        await SeedAccountsAsync(Path.Combine(dataDir, "sovrant.db"), opt.Users);
        Say($"Created {opt.Users} accounts in {sw.Elapsed.TotalSeconds:0.0}s");
    }

    var sampler = web is null ? null : new MemorySampler(web);
    var pages = new[] { "/chat", "/artifacts", "/settings", "/dashboard", "/command" };

    // Warm up: one user visits every page, so start-up cost (JIT, caches, first renders) isn't
    // counted as per-user memory.
    await using (var warm = new SimUser(baseUri, opt.Url is null ? "owner@example.com" : "load1@example.com"))
    {
        await warm.SignInAsync(Options.Password, CancellationToken.None);
        await warm.Circuit.StartAsync(warm.Http, "/dashboard", CancellationToken.None);
        foreach (var page in pages) await warm.Circuit.NavigateAsync(page, CancellationToken.None);
    }
    await Task.Delay(TimeSpan.FromSeconds(opt.SettleSeconds));
    var baseline = sampler?.SampleAfterFullGc();
    if (baseline is not null) Say($"Baseline (after warm-up, full GC): {baseline}");

    // ── Sign in + open circuits (ramped) ────────────────────────────────────────────────────────
    var users = new ConcurrentBag<SimUser>();
    var signInMs = new ConcurrentBag<double>();
    var circuitMs = new ConcurrentBag<double>();
    var failures = new ConcurrentDictionary<string, int>();
    using var cts = new CancellationTokenSource();
    var gate = new SemaphoreSlim(opt.Concurrency);
    var started = Stopwatch.StartNew();
    var tasks = new List<Task>();
    var steps = new List<(int Circuits, MemorySample Sample)>();
    if (baseline is not null) steps.Add((0, baseline));
    var stepSize = Math.Max(1, (int)Math.Ceiling(opt.Users / (double)opt.Steps));
    for (var i = 1; i <= opt.Users; i++)
    {
        var n = i;
        await gate.WaitAsync();
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                var u = new SimUser(baseUri, $"load{n}@example.com");
                var t = Stopwatch.StartNew();
                await u.SignInAsync(Options.Password, cts.Token);
                signInMs.Add(t.Elapsed.TotalMilliseconds);
                t.Restart();
                await u.Circuit.StartAsync(u.Http, "/dashboard", cts.Token);
                circuitMs.Add(t.Elapsed.TotalMilliseconds);
                users.Add(u);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures.AddOrUpdate(Short(ex), 1, (_, c) => c + 1);
            }
            finally { gate.Release(); }
        }));
        if (opt.RampPerSecond > 0) await Task.Delay(TimeSpan.FromSeconds(1.0 / opt.RampPerSecond));
        if (sampler is not null && (i % stepSize == 0 || i == opt.Users))
        {
            await Task.WhenAll(tasks);
            await Task.Delay(TimeSpan.FromSeconds(3));
            var sample = sampler.Sample();
            steps.Add((users.Count, sample));
            Say($"  {users.Count,5} circuits: {sample}");
        }
    }
    await Task.WhenAll(tasks);
    Say($"Opened {users.Count}/{opt.Users} circuits in {started.Elapsed.TotalSeconds:0.0}s" +
        (failures.IsEmpty ? "" : $" - failures: {string.Join("; ", failures.Select(f => $"{f.Value}× {f.Key}"))}"));
    Say($"Sign-in  {Stats(signInMs)}");
    Say($"Circuit  {Stats(circuitMs)}");

    // ── Hold, with optional clicking around ─────────────────────────────────────────────────────
    var navMs = new ConcurrentBag<double>();
    var navErrors = 0;
    var peak = sampler?.Sample();
    var hold = Stopwatch.StartNew();
    var clickers = users.Select((u, idx) => Task.Run(async () =>
    {
        var rnd = new Random(idx);
        while (hold.Elapsed.TotalSeconds < opt.HoldSeconds)
        {
            if (opt.NavigateEverySeconds <= 0) { await Task.Delay(1000); continue; }
            await Task.Delay(TimeSpan.FromSeconds(opt.NavigateEverySeconds * (0.5 + rnd.NextDouble())));
            if (hold.Elapsed.TotalSeconds >= opt.HoldSeconds) break;
            try
            {
                var t = Stopwatch.StartNew();
                await u.Circuit.NavigateAsync(pages[rnd.Next(pages.Length)], cts.Token);
                navMs.Add(t.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Interlocked.Increment(ref navErrors); }
        }
    })).ToList();
    while (hold.Elapsed.TotalSeconds < opt.HoldSeconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        var s = sampler?.Sample();
        if (s is not null && (peak is null || s.WorkingSetMb > peak.WorkingSetMb)) peak = s;
    }
    await Task.WhenAll(clickers);
    var loaded = sampler?.SampleAfterFullGc();
    var circuitErrors = users.Count(u => u.Circuit.Error is not null);
    Say($"Held {users.Count} circuits for {opt.HoldSeconds}s" +
        (opt.NavigateEverySeconds > 0 ? $" - {navMs.Count} page changes, {navErrors} failed; page change {Stats(navMs)}" : "") +
        $"; circuits with errors: {circuitErrors}");
    if (circuitErrors > 0)
        foreach (var g in users.Where(u => u.Circuit.Error is not null).GroupBy(u => u.Circuit.Error!).Take(5))
            Say($"   {g.Count()}× {g.Key}");
    if (loaded is not null && baseline is not null && users.Count > 0)
    {
        Say($"Loaded (full GC): {loaded}   (peak working set before GC {peak?.WorkingSetMb:0} MB)");
        Say($"Average per circuit after holding: {(loaded.WorkingSetMb - baseline.WorkingSetMb) * 1024 / users.Count:0} KB working set, " +
            $"{(loaded.PrivateMb - baseline.PrivateMb) * 1024 / users.Count:0} KB private");
        if (steps.Count >= 3)
        {
            // Least-squares slope of private memory against open circuits: the cost of one more user,
            // without the fixed overhead.
            var xs = steps.Select(s => (double)s.Circuits).ToArray();
            var ys = steps.Select(s => s.Sample.PrivateMb).ToArray();
            double mx = xs.Average(), my = ys.Average();
            var slope = xs.Zip(ys).Sum(p => (p.First - mx) * (p.Second - my)) / xs.Sum(x => (x - mx) * (x - mx));
            Say($"Marginal cost while opening: {slope * 1024:0} KB private per circuit ({slope * 100:0} MB per 100 users)");
        }
    }

    // ── Close everything; circuits are kept for Blazor's disconnect retention period ────────────
    foreach (var u in users) await u.DisposeAsync();
    await Task.Delay(TimeSpan.FromSeconds(opt.SettleSeconds));
    var after = sampler?.SampleAfterFullGc();
    if (after is not null) Say($"After closing: {after}  (disconnected circuits are kept ~3 min for reconnects)");
}
finally
{
    if (web is not null && !web.HasExited)
    {
        web.Kill(entireProcessTree: true);
        await web.WaitForExitAsync();
    }
    var report = Path.Combine(opt.OutDir, $"loadtest-{opt.Users}u-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    Directory.CreateDirectory(opt.OutDir);
    await File.WriteAllTextAsync(report, $"Sovrant.Web load test — {DateTime.Now.ToString("u", inv)}\n" +
        $"Machine: {Environment.MachineName}, {Environment.ProcessorCount} logical CPUs, {Environment.OSVersion}\n" +
        $"Options: {string.Join(' ', args)}\n\n" + log);
    Console.WriteLine($"Report: {report}");
    if (!opt.KeepData && Directory.Exists(dataDir))
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { }
}

static string Short(Exception ex) => ex.GetType().Name + ": " + (ex.Message.Length > 120 ? ex.Message[..120] : ex.Message);

static string Stats(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToArray();
    if (v.Length == 0) return "n/a";
    double P(double p) => v[Math.Min(v.Length - 1, (int)Math.Ceiling(p * v.Length) - 1)];
    return string.Create(CultureInfo.InvariantCulture, $"p50 {P(0.50):0} ms | p95 {P(0.95):0} ms | max {v[^1]:0} ms (n={v.Length})");
}

static Process StartWeb(Options opt, string dataDir)
{
    var dll = opt.WebDll ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "src", "Sovrant.Web", "bin", opt.Configuration, "net10.0", "Sovrant.Web.dll"));
    if (!File.Exists(dll))
        throw new FileNotFoundException($"Build Sovrant.Web first ({opt.Configuration}), or pass --web <path to Sovrant.Web.dll>.", dll);
    var psi = new ProcessStartInfo("dotnet", $"\"{dll}\"")
    {
        WorkingDirectory = Path.GetDirectoryName(dll)!,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    psi.Environment["SOVRANT_DB_PATH"] = Path.Combine(dataDir, "sovrant.db");
    psi.Environment["SOVRANT_WEB_PORT"] = opt.Port.ToString(CultureInfo.InvariantCulture);
    psi.Environment["SOVRANT_RUNTIME_MODE"] = "embedded";
    psi.Environment["SOVRANT_LOG_FILE"] = Path.Combine(dataDir, "web.log");
    psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    // "server" is ASP.NET Core's default (one heap per CPU core, grows eagerly); "workstation" keeps
    // the heap close to what's live, so it shows the real cost of each user more clearly.
    if (opt.Gc is not null) psi.Environment["DOTNET_gcServer"] = opt.Gc == "workstation" ? "0" : "1";
    // Like a container memory limit: the GC must keep the heap under this, so a run that succeeds
    // proves the server fits in it.
    if (opt.HeapLimitMb > 0)
        psi.Environment["DOTNET_GCHeapHardLimit"] = "0x" + ((long)opt.HeapLimitMb * 1024 * 1024).ToString("X", CultureInfo.InvariantCulture);
    psi.Environment["HOME"] = dataDir;          // keep ~/.sovrant out of the real profile
    psi.Environment["USERPROFILE"] = dataDir;
    var p = Process.Start(psi)!;
    p.OutputDataReceived += (_, _) => { };
    p.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine("[web] " + e.Data); };
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    return p;
}

static async Task WaitReadyAsync(Uri baseUri)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < TimeSpan.FromMinutes(3))
    {
        try
        {
            if ((await http.GetAsync(new Uri(baseUri, "ready"))).IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(500);
    }
    throw new TimeoutException($"{baseUri} didn't become ready");
}

static async Task SeedAccountsAsync(string dbPath, int count)
{
    Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", dbPath);
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSovrantRuntime(new SovrantConfig(), BootstrapConfigLoader.Load());
    await using var sp = services.BuildServiceProvider();
    var identity = sp.GetRequiredService<IIdentityService>();
    if (await identity.IsFirstRunAsync())
        await identity.RegisterAsync("owner@example.com", Options.Password, issueToken: false);
    await identity.SetRegistrationOpenAsync(true);
    await identity.SetApprovalRequiredAsync(false);
    for (var i = 1; i <= count; i++)
    {
        var r = await identity.RegisterAsync($"load{i}@example.com", Options.Password, issueToken: false);
        if (!r.Success) throw new InvalidOperationException($"Couldn't create load{i}: {r.Error}");
    }
    await identity.SetRegistrationOpenAsync(false);
}

/// <summary>One simulated person: a cookie jar, a browser-like HTTP client and one circuit.</summary>
sealed partial class SimUser : IAsyncDisposable
{
    private readonly Uri _baseUri;
    private readonly string _email;
    public CookieContainer Cookies { get; } = new();
    public HttpClient Http { get; }
    public BlazorCircuit Circuit { get; }

    public SimUser(Uri baseUri, string email)
    {
        _baseUri = baseUri;
        _email = email;
        Http = new HttpClient(new HttpClientHandler { CookieContainer = Cookies, AllowAutoRedirect = false }) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(60) };
        Circuit = new BlazorCircuit(baseUri, Cookies);
    }

    public async Task SignInAsync(string password, CancellationToken ct)
    {
        var page = await Http.GetStringAsync(new Uri("/login", UriKind.Relative), ct);
        var token = TokenRegex().Match(page).Groups[1].Value;
        if (token.Length == 0) throw new InvalidOperationException("no antiforgery token on /login");
        using var form = new FormUrlEncodedContent([new("email", _email), new("password", password), new("__RequestVerificationToken", token)]);
        var response = await Http.PostAsync(new Uri("/auth/login", UriKind.Relative), form, ct);
        var location = response.Headers.Location?.OriginalString;
        if (location != "/dashboard") throw new InvalidOperationException($"sign-in failed ({(int)response.StatusCode} → {location})");
    }

    public async ValueTask DisposeAsync()
    {
        await Circuit.DisposeAsync();
        Http.Dispose();
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}

sealed record MemorySample(double WorkingSetMb, double PrivateMb, int Threads, int Handles)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"working set {WorkingSetMb:0} MB, private {PrivateMb:0} MB, threads {Threads}, handles {Handles}");
}

sealed class MemorySampler(Process process)
{
    /// <summary>
    /// Makes the server run a full, blocking garbage collection (an EventPipe session with the
    /// GCHeapCollect keyword, as dotnet-gcdump does), then samples — so the numbers show memory
    /// that's actually in use, not garbage the collector hasn't got round to yet.
    /// </summary>
    public MemorySample SampleAfterFullGc()
    {
        try
        {
            var client = new Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient(process.Id);
            var provider = new Microsoft.Diagnostics.NETCore.Client.EventPipeProvider(
                "Microsoft-Windows-DotNETRuntime", System.Diagnostics.Tracing.EventLevel.Informational,
                keywords: 0x1 | 0x800000); // GC | GCHeapCollect
            using var session = client.StartEventPipeSession(provider, requestRundown: false);
            var drain = Task.Run(() => { try { session.EventStream.CopyTo(Stream.Null); } catch (IOException) { } });
            Thread.Sleep(3000);
            session.Stop();
            drain.Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine("  (couldn't force a GC: " + ex.Message + ")");
        }
        Thread.Sleep(1000);
        return Sample();
    }

    public MemorySample Sample()
    {
        process.Refresh();
        return new MemorySample(process.WorkingSet64 / 1048576.0, process.PrivateMemorySize64 / 1048576.0,
            process.Threads.Count, process.HandleCount);
    }
}

sealed class Options
{
    public const string Password = "load-test-password-1";
    public int Users { get; private set; } = 100;
    public int Concurrency { get; private set; } = 20;
    public double RampPerSecond { get; private set; } = 20;
    public int HoldSeconds { get; private set; } = 60;
    public double NavigateEverySeconds { get; private set; } = 15;
    public int SettleSeconds { get; private set; } = 10;
    public int Steps { get; private set; } = 5;
    public int Port { get; private set; } = 5190;
    public Uri? Url { get; private set; }
    public string? WebDll { get; private set; }
    public string Configuration { get; private set; } = "Release";
    public string OutDir { get; private set; } = Path.Combine(Directory.GetCurrentDirectory(), "loadtest-results");
    public bool KeepData { get; private set; }
    public string? Gc { get; private set; }
    public int HeapLimitMb { get; private set; }

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            var inv = CultureInfo.InvariantCulture;
            switch (args[i])
            {
                case "--users": o.Users = int.Parse(Next(), inv); break;
                case "--concurrency": o.Concurrency = int.Parse(Next(), inv); break;
                case "--ramp": o.RampPerSecond = double.Parse(Next(), inv); break;
                case "--hold": o.HoldSeconds = int.Parse(Next(), inv); break;
                case "--navigate-every": o.NavigateEverySeconds = double.Parse(Next(), inv); break;
                case "--settle": o.SettleSeconds = int.Parse(Next(), inv); break;
                case "--steps": o.Steps = int.Parse(Next(), inv); break;
                case "--port": o.Port = int.Parse(Next(), inv); break;
                case "--url": o.Url = new Uri(Next().TrimEnd('/') + "/"); break;
                case "--web": o.WebDll = Next(); break;
                case "--configuration": o.Configuration = Next(); break;
                case "--out": o.OutDir = Next(); break;
                case "--keep-data": o.KeepData = true; break;
                case "--heap-limit-mb": o.HeapLimitMb = int.Parse(Next(), inv); break;
                case "--gc": o.Gc = Next() is "workstation" or "server" ? args[i] : throw new ArgumentException("--gc is server or workstation"); break;
                default: throw new ArgumentException($"Unknown option {args[i]} (see tools/Sovrant.LoadTest/README.md)");
            }
        }
        return o;
    }
}
