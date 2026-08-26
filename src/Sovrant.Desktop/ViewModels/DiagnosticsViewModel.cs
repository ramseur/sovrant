using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;

namespace Sovrant.Desktop.ViewModels;

public partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly ISmartRouter _router;
    private readonly SovrantConfig _config;
    private readonly BootstrapConfig _bootstrap;
    private readonly IHttpClientFactory _httpFactory;

    [ObservableProperty]
    private bool _isRunning;

    // Connection
    [ObservableProperty]
    private string _connectionStatus = "Checking...";

    [ObservableProperty]
    private bool _isConnected;

    // Model info
    [ObservableProperty]
    private string _currentModel = string.Empty;

    [ObservableProperty]
    private string _currentProvider = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private int _maxTokens;

    // System info
    [ObservableProperty]
    private string _runtimeVersion = string.Empty;

    [ObservableProperty]
    private string _dotNetVersion = string.Empty;

    [ObservableProperty]
    private string _cSharpVersion = string.Empty;

    [ObservableProperty]
    private string _osInfo = string.Empty;

    [ObservableProperty]
    private string _dbPath = string.Empty;

    [ObservableProperty]
    private string _dbStatus = "Checking...";

    [ObservableProperty]
    private string _artifactsPath = string.Empty;

    // Provider health
    public ObservableCollection<ProviderHealthItem> Providers { get; } = [];

    // Health checks
    public ObservableCollection<HealthCheckItem> HealthChecks { get; } = [];

    public DiagnosticsViewModel(ISmartRouter router, SovrantConfig config, BootstrapConfig bootstrap, IHttpClientFactory httpFactory)
    {
        _router = router;
        _config = config;
        _bootstrap = bootstrap;
        _httpFactory = httpFactory;
        LoadSystemInfo();
        _ = RunAllChecksAsync();
    }

    private void LoadSystemInfo()
    {
        CurrentModel = _config.Model;
        MaxTokens = _config.MaxTokens;
        BaseUrl = _config.BaseUrl?.ToString() ?? "(default)";

        // Infer provider from base URL
        var url = _config.BaseUrl?.ToString() ?? string.Empty;
        CurrentProvider = url switch
        {
            _ when url.Contains("openrouter", StringComparison.OrdinalIgnoreCase) => "OpenRouter",
            _ when url.Contains("deepseek", StringComparison.OrdinalIgnoreCase) => "DeepSeek",
            _ when url.Contains("groq", StringComparison.OrdinalIgnoreCase) => "Groq",
            _ when url.Contains("mistral", StringComparison.OrdinalIgnoreCase) => "Mistral",
            _ when url.Contains("together", StringComparison.OrdinalIgnoreCase) => "Together AI",
            _ when url.Contains("localhost:11434", StringComparison.Ordinal) => "Ollama",
            _ when url.Contains("localhost:1234", StringComparison.Ordinal) => "LM Studio",
            _ when string.IsNullOrEmpty(url) => "OpenAI",
            _ => "Custom",
        };

        // System
        var asm = typeof(SovrantConfig).Assembly;
        RuntimeVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString() ?? "unknown";
        DotNetVersion = RuntimeInformation.FrameworkDescription;
        CSharpVersion = $"C# {Environment.Version.Major + 4}";
        OsInfo = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

        // Paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DbPath = _bootstrap.DbPath ?? Path.Combine(home, ".sovrant", "data", "sovrant.db");
        ArtifactsPath = _bootstrap.ArtifactsRoot ?? Path.Combine(home, ".sovrant", "workspaces");
    }

    [RelayCommand]
    private async Task RunAllChecksAsync()
    {
        IsRunning = true;
        HealthChecks.Clear();
        Providers.Clear();

        // API key
        var hasKey = !string.IsNullOrWhiteSpace(_config.ApiKey);
        HealthChecks.Add(new HealthCheckItem
        {
            Name = "API key",
            Status = hasKey ? "PASS" : "FAIL",
            IsHealthy = hasKey,
            Detail = hasKey ? "Configured" : "No API key set — chat will not work",
        });

        // 3. Database
        var dbExists = File.Exists(DbPath);
        DbStatus = dbExists ? "OK" : "Missing";
        HealthChecks.Add(new HealthCheckItem
        {
            Name = "Database",
            Status = dbExists ? "PASS" : "WARN",
            IsHealthy = dbExists,
            Detail = dbExists ? $"{new FileInfo(DbPath).Length / 1024} KB" : "Database file not found",
        });

        // 4. Artifacts directory
        var artifactsExists = Directory.Exists(ArtifactsPath);
        HealthChecks.Add(new HealthCheckItem
        {
            Name = "Artifacts directory",
            Status = artifactsExists ? "PASS" : "WARN",
            IsHealthy = artifactsExists,
            Detail = artifactsExists ? ArtifactsPath : "Will be created on first artifact",
        });

        // 5. API connectivity
        ConnectionStatus = "Checking...";
        IsConnected = false;
        if (hasKey && _config.BaseUrl is not null)
        {
            try
            {
                using var http = _httpFactory.CreateClient("ProviderProbe");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
                var modelsUrl = _config.BaseUrl.ToString().TrimEnd('/') + "/models";
                var response = await http.GetAsync(new Uri(modelsUrl), cts.Token);
                IsConnected = response.IsSuccessStatusCode;
                ConnectionStatus = IsConnected ? "Connected" : $"HTTP {(int)response.StatusCode}";
                HealthChecks.Add(new HealthCheckItem
                {
                    Name = "API connectivity",
                    Status = IsConnected ? "PASS" : "FAIL",
                    IsHealthy = IsConnected,
                    Detail = IsConnected ? $"Reached {_config.BaseUrl.Host}" : $"HTTP {(int)response.StatusCode} from {_config.BaseUrl.Host}",
                });
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Unreachable";
                HealthChecks.Add(new HealthCheckItem
                {
                    Name = "API connectivity",
                    Status = "FAIL",
                    IsHealthy = false,
                    Detail = ex.Message,
                });
            }
        }
        else
        {
            ConnectionStatus = hasKey ? "No base URL" : "No API key";
            HealthChecks.Add(new HealthCheckItem
            {
                Name = "API connectivity",
                Status = "SKIP",
                IsHealthy = false,
                Detail = hasKey ? "No base URL configured" : "No API key configured — skipped",
            });
        }

        // 6. Recent log errors
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "logs");
        var todayLog = Path.Combine(logDir, $"sovrant-{DateTime.Now:yyyyMMdd}.log");
        var errorCount = 0;
        if (File.Exists(todayLog))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(todayLog);
                errorCount = lines.Count(l => l.Contains("[Error", StringComparison.Ordinal));
            }
            catch { /* best effort */ }
        }
        HealthChecks.Add(new HealthCheckItem
        {
            Name = "Today's errors",
            Status = errorCount == 0 ? "PASS" : "WARN",
            IsHealthy = errorCount == 0,
            Detail = errorCount == 0 ? "No errors in today's log" : $"{errorCount} error(s) in {Path.GetFileName(todayLog)}",
        });

        // Provider health — ping each provider's actual endpoint to verify connectivity.
        var statuses = _router.GetStatus();
        foreach (var s in statuses)
        {
            // Map provider name to its actual base URL for pinging.
            var providerUrl = s.Name.Equals("ollama", StringComparison.OrdinalIgnoreCase)
                ? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434/v1"
                : _config.BaseUrl?.ToString();

            var item = new ProviderHealthItem
            {
                Name = s.Name,
                LatencyMs = s.RequestCount > 0 ? $"{s.LatencyMs:F0}ms" : "—",
                RequestCount = s.RequestCount,
                ErrorCount = s.ErrorCount,
                ErrorRate = s.ErrorRate,
                Score = s.Score,
            };

            if (!string.IsNullOrEmpty(providerUrl))
            {
                try
                {
                    using var http = _httpFactory.CreateClient("ProviderProbe");
                    using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    // Only add auth for non-local providers.
                    if (!providerUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(_config.ApiKey))
                        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");

                    // Ollama uses /api/tags, everything else uses /models.
                    var pingPath = s.Name.Equals("ollama", StringComparison.OrdinalIgnoreCase)
                        ? "/api/tags" : "/models";
                    var pingUrl = new Uri(new Uri(providerUrl.TrimEnd('/') + "/"), pingPath.TrimStart('/'));

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var resp = await http.GetAsync(pingUrl, cts2.Token);
                    sw.Stop();
                    item.IsHealthy = resp.IsSuccessStatusCode;
                    item.Status = resp.IsSuccessStatusCode ? "Connected" : "Not Connected";
                    item.LatencyMs = $"{sw.ElapsedMilliseconds}ms";
                }
                catch
                {
                    item.IsHealthy = false;
                    item.Status = "Not Connected";
                }
            }
            else
            {
                item.IsHealthy = false;
                item.Status = "Not Connected";
            }

            Providers.Add(item);
        }

        IsRunning = false;
    }
}

public partial class HealthCheckItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isHealthy;

    [ObservableProperty]
    private string _detail = string.Empty;
}

public partial class ProviderHealthItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isHealthy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _latencyMs = "—";

    [ObservableProperty]
    private int _requestCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private string _errorRate = "0%";

    [ObservableProperty]
    private string _score = "N/A";
}
