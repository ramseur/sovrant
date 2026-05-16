using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Capabilities;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    private readonly ISessionStore _sessionStore;
    private readonly SovrantConfig _config;
    private readonly MutableAuthProvider? _authProvider;
    private readonly IHttpClientFactory? _httpFactory;
    private readonly IUserPreferenceStore _prefs;
    private readonly IProviderProfileStore _profileStore;
    private readonly ICredentialStore _credentials;
    private readonly IModelCapabilityRegistry? _capabilityRegistry;
    private bool _suppressProfileSwitch;

    public ActiveContextViewModel ActiveContext { get; }

    [ObservableProperty]
    private string _selectedNavItem = "Chat";

    [ObservableProperty]
    private bool _isCollapsed;

    [ObservableProperty]
    private string _connectionStatus = "Connected";

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private string _currentModel = string.Empty;

    [ObservableProperty]
    private string _currentProvider = string.Empty;

    [ObservableProperty]
    private string _currentUserName = Environment.UserName;

    [ObservableProperty]
    private string _userInitial = Environment.UserName.Length > 0
        ? Environment.UserName[..1].ToUpperInvariant()
        : "U";

    [ObservableProperty]
    private ProviderProfileEntry? _selectedProfile;

    [ObservableProperty]
    private bool _isDropdownOpen;

    [ObservableProperty]
    private ProviderTreeGroup? _selectedTreeGroup;

    public ObservableCollection<ProviderProfileEntry> ProviderProfiles { get; } = [];
    public ObservableCollection<ProviderTreeGroup> TreeGroups { get; } = [];
    public ObservableCollection<SessionListItem> RecentSessions { get; } = [];

    /// <summary>True when showing providers list (step 1), false when showing models (step 2).</summary>
    public bool IsProviderStep => SelectedTreeGroup is null;

    /// <summary>True when at least one provider profile is configured. Drives the
    /// empty-state shown in the dropdown when the user has not added any provider yet.</summary>
    public bool HasProviderProfiles => ProviderProfiles.Count > 0;

    private static readonly Dictionary<string, string[]> StaticProviderModels = new(StringComparer.Ordinal)
    {
        ["OpenAI"] = ["gpt-5", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o", "gpt-4o-mini", "o4-mini", "o3", "o3-mini", "o1", "o1-mini"],
        // OpenRouter intentionally omitted — live fetch returns the full catalog, including free and paid.
        ["DeepSeek"] = ["deepseek-chat", "deepseek-reasoner"],
        ["Groq"] = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it"],
        ["Mistral"] = ["mistral-large-latest", "mistral-medium-latest", "mistral-small-latest", "open-mixtral-8x22b"],
        ["Together AI"] = ["meta-llama/Llama-3.3-70B-Instruct-Turbo", "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1", "Qwen/Qwen2.5-72B-Instruct-Turbo"],
        ["Google"] = ["gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-flash", "gemini-2.0-flash-lite"],
        ["Azure OpenAI"] = ["gpt-4o", "gpt-4o-mini", "gpt-4.1"],
    };

    public event EventHandler<string>? NavigationRequested;
    public event EventHandler<string>? SessionResumeRequested;

    /// <summary>Updates the user chip in the sidebar after login.</summary>
    public void SetCurrentUser(string emailOrId)
    {
        // Use the local part of the email (before @) as the display name if it's an email.
        var atIdx = emailOrId.IndexOf('@', StringComparison.Ordinal);
        CurrentUserName = atIdx > 0 ? emailOrId[..atIdx] : emailOrId;
        UserInitial = CurrentUserName.Length > 0 ? CurrentUserName[..1].ToUpperInvariant() : "U";
    }

    public SidebarViewModel(
        ISessionStore sessionStore,
        SovrantConfig config,
        ActiveContextViewModel activeContext,
        IUserPreferenceStore prefs,
        IProviderProfileStore profileStore,
        ICredentialStore credentials,
        MutableAuthProvider? authProvider = null,
        IHttpClientFactory? httpFactory = null,
        IModelCapabilityRegistry? capabilityRegistry = null)
    {
        _sessionStore = sessionStore;
        _config = config;
        _authProvider = authProvider;
        _httpFactory = httpFactory;
        _prefs = prefs;
        _profileStore = profileStore;
        _credentials = credentials;
        _capabilityRegistry = capabilityRegistry;
        ActiveContext = activeContext;
        // Re-fire PropertyChanged for HasProviderProfiles whenever the collection
        // changes so the dropdown's empty-state toggles correctly.
        ProviderProfiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProviderProfiles));
        LoadFromConfig(config);
        _ = LoadProviderProfilesAsync().ContinueWith(
            t => System.Diagnostics.Debug.WriteLine($"[SidebarViewModel] LoadProviderProfilesAsync failed: {t.Exception}"),
            System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        _ = LoadSessionsAsync().ContinueWith(
            t => System.Diagnostics.Debug.WriteLine($"[SidebarViewModel] LoadSessionsAsync failed: {t.Exception}"),
            System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    partial void OnSelectedTreeGroupChanged(ProviderTreeGroup? value)
    {
        OnPropertyChanged(nameof(IsProviderStep));
    }

    partial void OnSelectedProfileChanged(ProviderProfileEntry? value)
    {
        if (value is null || _suppressProfileSwitch) return;
        SwitchToProfile(value);
    }

    [RelayCommand]
    private void ToggleDropdown()
    {
        IsDropdownOpen = !IsDropdownOpen;
        if (!IsDropdownOpen) SelectedTreeGroup = null;
    }

    [RelayCommand]
    private async Task SelectProviderAsync(ProviderTreeGroup group)
    {
        SelectedTreeGroup = group;
        // Always prefer the provider's live model list — the static dictionary is just an offline
        // fallback. Fetch once per session per provider; static list stays visible until live arrives.
        if (!group.ModelsFetchedLive && !string.IsNullOrWhiteSpace(group.BaseUrl) && _httpFactory is not null)
        {
            // Re-retrieve the credential in case it wasn't available at startup.
            var apiKey = group.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(group.CredentialId))
            {
                var raw = await _credentials.RetrieveAsync(group.CredentialId) ?? string.Empty;
                apiKey = new string(raw.Where(c => c < 128).ToArray()).Trim();
            }
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            // Mark as fetched before the await so concurrent calls don't double-fetch.
            group.ModelsFetchedLive = true;
            var fetched = await FetchModelIdsAsync(group.BaseUrl, apiKey);
            var filtered = FilterChatModels(group.Provider, fetched);
            if (filtered.Count > 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    group.Models.Clear();
                    foreach (var m in filtered)
                        group.Models.Add(new ModelOption(m, group, IsFreeModel(m)));
                    group.ModelCount = group.Models.Count;
                });
            }
            else
            {
                // Fetch returned nothing useful — allow a retry next time.
                group.ModelsFetchedLive = false;
            }
        }
    }

    /// <summary>
    /// Drop obvious non-chat models from a live provider response (embeddings, TTS, image gen,
    /// moderation, whisper, legacy completion). Safe default: keep unknown entries.
    /// </summary>
    private static List<string> FilterChatModels(string provider, List<string> ids)
    {
        if (ids.Count == 0) return ids;
        var excludes = new[]
        {
            "embedding", "embed-", "-embed",
            "dall-e", "dalle", "image-",
            "whisper", "tts-", "-tts",
            "moderation",
            "realtime-preview", "transcribe",
            "babbage", "ada-00", "curie", "davinci",
        };
        return ids.Where(id =>
        {
            foreach (var ex in excludes)
                if (id.Contains(ex, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToList();
    }

    private async Task<List<string>> FetchModelIdsAsync(string baseUrl, string apiKey)
    {
        try
        {
            using var http = _httpFactory!.CreateClient("ProviderProbe");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var modelsUrl = baseUrl.TrimEnd('/') + "/models";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(modelsUrl));
            var safeKey = new string(apiKey.Where(c => c < 128).ToArray()).Trim();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", safeKey);
            var response = await http.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetProperty("id", out var id) && id.GetString() is { } modelId && seen.Add(modelId))
                        models.Add(modelId);
            models.Sort(StringComparer.OrdinalIgnoreCase);
            return models;
        }
        catch { return []; }
    }

    [RelayCommand]
    private void BackToProviders()
    {
        SelectedTreeGroup = null;
    }

    [RelayCommand]
    private async Task SelectModelAsync(ModelOption option)
    {
        if (option.Group is null) return;
        var group = option.Group;

        // Retrieve credential on demand — never carried in view-model state.
        var rawKey = string.IsNullOrWhiteSpace(group.CredentialId)
            ? string.Empty
            : await _credentials.RetrieveAsync(group.CredentialId) ?? string.Empty;
        var apiKey = new string(rawKey.Where(c => c < 128).ToArray()).Trim();

        // Hot-swap runtime config.
        _config.ApiKey = apiKey;
        _config.MaxTokens = group.MaxTokens;
        _config.Model = option.Model;

        if (_authProvider is not null)
            _authProvider.ApiKey = apiKey;

        if (!string.IsNullOrWhiteSpace(group.BaseUrl))
        {
            var parsed = new Uri(group.BaseUrl);
            _config.BaseUrl = parsed;
            if (_authProvider is not null) _authProvider.BaseUrl = parsed;
        }
        else
        {
            _config.BaseUrl = null;
            if (_authProvider is not null) _authProvider.BaseUrl = null;
        }

        // Update display.
        CurrentModel = ShortenModelName(option.Model);
        CurrentProvider = group.Provider;
        IsConnected = !string.IsNullOrWhiteSpace(apiKey);
        ConnectionStatus = IsConnected ? "Connected" : "No API key";
        IsDropdownOpen = false;
        SelectedTreeGroup = null;

        await PersistActiveProfileAsync(new ProviderProfileEntry
        {
            ProfileId = group.ProfileId,
            CredentialId = group.CredentialId,
            Provider = group.Provider,
            Model = option.Model,
            BaseUrl = group.BaseUrl,
            MaxTokens = group.MaxTokens,
        });
    }

    private async void SwitchToProfile(ProviderProfileEntry profile)
    {
        try
        {
            // Retrieve the credential on demand — view-model state never carries
            // a plaintext key between user interactions.
            var rawKey = string.IsNullOrWhiteSpace(profile.CredentialId)
                ? string.Empty
                : await _credentials.RetrieveAsync(profile.CredentialId) ?? string.Empty;
            var apiKey = new string(rawKey.Where(c => c < 128).ToArray()).Trim();

            // Hot-swap runtime config.
            _config.ApiKey = apiKey;
            _config.MaxTokens = profile.MaxTokens;

            if (_authProvider is not null)
                _authProvider.ApiKey = apiKey;

            if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
            {
                var parsed = new Uri(profile.BaseUrl);
                _config.BaseUrl = parsed;
                if (_authProvider is not null) _authProvider.BaseUrl = parsed;
            }
            else
            {
                _config.BaseUrl = null;
                if (_authProvider is not null) _authProvider.BaseUrl = null;
            }

            if (!string.IsNullOrWhiteSpace(profile.Model))
                _config.Model = profile.Model;

            // Update display.
            CurrentModel = ShortenModelName(_config.Model);
            CurrentProvider = profile.Provider;
            IsConnected = !string.IsNullOrWhiteSpace(apiKey);
            ConnectionStatus = IsConnected ? "Connected" : "No API key";

            await PersistActiveProfileAsync(profile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SidebarViewModel] SwitchToProfile failed: {ex}");
        }
    }

    public async Task LoadProviderProfilesAsync()
    {
        // Credentials are NOT loaded here — retrieved on demand at activate-time
        // so plaintext keys don't sit in view-model state for the process lifetime.
        var rows = await _profileStore.ListUserAndWorkspaceAsync(
            App.SovrantUserId, ActiveContext.ActiveWorkspaceId);
        var entries = new List<ProviderProfileEntry>(rows.Count);
        foreach (var row in rows)
        {
            entries.Add(new ProviderProfileEntry
            {
                ProfileId = row.ProfileId,
                CredentialId = row.CredentialId,
                Name = row.Name,
                Provider = row.ProviderKind,
                Model = row.DefaultModel ?? string.Empty,
                ApiKey = string.Empty,
                BaseUrl = row.BaseUrl,
                MaxTokens = row.MaxTokens ?? 32000,
                IsWorkspaceProfile = row.IsAdminManaged,
            });
        }

        var savedProfileId = await _prefs.GetAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId)
            .ConfigureAwait(false);
        var savedModel = await _prefs.GetAsync(App.SovrantUserId, UserPreferenceKeys.Model)
            .ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProviderProfiles.Clear();
            TreeGroups.Clear();
            foreach (var p in entries)
                ProviderProfiles.Add(p);

            _suppressProfileSwitch = true;

            // Match the saved profile id against the loaded set. No fallback by
            // provider name — that auto-picks across deletions which surprises users.
            var matched = !string.IsNullOrEmpty(savedProfileId)
                ? ProviderProfiles.FirstOrDefault(p => p.ProfileId == savedProfileId)
                : null;
            SelectedProfile = matched;

            // Three-state display, driven by profile + model-pref state (NOT
            // SovrantConfig.Model — that carries the hardcoded "gpt-4o-mini"
            // bootstrap default and a stale legacy LlmApiKey credential can keep
            // ApiKey populated even after the user deletes their provider).
            if (matched is null)
            {
                CurrentModel = "No model";
                CurrentProvider = ProviderProfiles.Count == 0 ? "Add a provider" : "Select a provider";
                IsConnected = false;
                ConnectionStatus = ProviderProfiles.Count == 0 ? "No provider" : "No provider selected";
            }
            else if (string.IsNullOrWhiteSpace(savedModel))
            {
                CurrentModel = "Select a model";
                CurrentProvider = matched.Provider;
                IsConnected = false;
                ConnectionStatus = "Choose a model";
            }
            else
            {
                CurrentModel = ShortenModelName(savedModel);
                CurrentProvider = matched.Provider;
                IsConnected = !string.IsNullOrWhiteSpace(_config.ApiKey);
                ConnectionStatus = IsConnected ? "Connected" : "No API key";
            }

            _suppressProfileSwitch = false;

            BuildTreeGroups();
        });
    }

    private bool IsFreeModel(string modelId)
    {
        if (modelId.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            return true;
        if (_capabilityRegistry is null)
            return false;
        var caps = _capabilityRegistry.GetCapabilities(modelId);
        return caps.CostPerMillionInput is 0m && caps.CostPerMillionOutput is 0m;
    }

    private void BuildTreeGroups()
    {
        TreeGroups.Clear();
        var groups = new Dictionary<string, ProviderTreeGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in ProviderProfiles)
        {
            if (!groups.TryGetValue(profile.Provider, out var group))
            {
                group = new ProviderTreeGroup
                {
                    Provider = profile.Provider,
                    ProfileId = profile.ProfileId,
                    CredentialId = profile.CredentialId,
                    BaseUrl = profile.BaseUrl,
                    MaxTokens = profile.MaxTokens,
                    IsWorkspaceProfile = profile.IsWorkspaceProfile,
                };
                if (StaticProviderModels.TryGetValue(profile.Provider, out var staticModels))
                    foreach (var m in staticModels) group.Models.Add(new ModelOption(m, group, IsFreeModel(m)));
                groups[profile.Provider] = group;
            }
            // Only seed the saved model as a fallback for providers with a static list.
            // Providers like OpenRouter will fetch live — their list should start empty.
            if (!string.IsNullOrWhiteSpace(profile.Model)
                && StaticProviderModels.ContainsKey(profile.Provider)
                && !group.Models.Any(mo => mo.Model.Equals(profile.Model, StringComparison.OrdinalIgnoreCase)))
            {
                group.Models.Insert(0, new ModelOption(profile.Model, group, IsFreeModel(profile.Model)));
            }
        }
        foreach (var g in groups.Values)
        {
            g.IsCurrent = g.Provider.Equals(CurrentProvider, StringComparison.OrdinalIgnoreCase);
            g.ModelCount = g.Models.Count;
            TreeGroups.Add(g);
        }
    }

    private async Task PersistActiveProfileAsync(ProviderProfileEntry profile)
    {
        try
        {
            // Pin this profile id as the active one so the next boot's
            // ApplyUserPreferencesAsync hydrates the same provider.
            if (!string.IsNullOrEmpty(profile.ProfileId))
                await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId, profile.ProfileId);

            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Provider, profile.Provider);

            if (!string.IsNullOrWhiteSpace(profile.Model))
                await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Model, profile.Model);
            if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
                await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.BaseUrl, profile.BaseUrl);

            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.MaxTokens,
                profile.MaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Refresh the credential under the global key so the next boot's
            // fallback path (no profile) finds the same value too.
            if (!string.IsNullOrWhiteSpace(profile.CredentialId))
            {
                var stored = await _credentials.RetrieveAsync(profile.CredentialId);
                if (!string.IsNullOrWhiteSpace(stored))
                    await _credentials.StoreAsync(Sovrant.Api.Auth.CredentialKeys.LlmApiKey, stored);
            }
        }
        catch { /* best effort */ }
    }

    /// <summary>Sets a safe initial display state. The real values are populated
    /// by <see cref="LoadProviderProfilesAsync"/> once profile + model-pref state
    /// is loaded from the DB. We deliberately don't read <c>config.Model</c> /
    /// <c>config.ApiKey</c> here: the former carries a hardcoded "gpt-4o-mini"
    /// bootstrap default and the latter can be a stale legacy <c>LlmApiKey</c>
    /// credential that outlives an active provider profile.</summary>
    public void LoadFromConfig(SovrantConfig config)
    {
        CurrentModel = "No model";
        CurrentProvider = "Loading...";
        IsConnected = false;
        ConnectionStatus = "Loading...";
    }

    /// <summary>
    /// Shortens a model ID like "google/gemma-4-26b-a4b-it:free" to "gemma-4-26b-a4b-it".
    /// Strips the provider prefix and common suffixes like ":free".
    /// </summary>
    internal static string ShortenModelName(string model)
    {
        if (string.IsNullOrEmpty(model)) return model;

        // Strip provider prefix (e.g. "google/", "meta-llama/")
        var slashIdx = model.IndexOf('/', StringComparison.Ordinal);
        if (slashIdx >= 0 && slashIdx < model.Length - 1)
            model = model[(slashIdx + 1)..];

        // Strip common suffixes
        foreach (var suffix in new[] { ":free", ":extended" })
        {
            if (model.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                model = model[..^suffix.Length];
                break;
            }
        }

        return model;
    }

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void Navigate(string pageName)
    {
        SelectedNavItem = pageName;
        NavigationRequested?.Invoke(this, pageName);
    }

    /// <summary>Empty-state action in the model dropdown: close the popup and
    /// navigate the user to the Settings page so they can configure a provider.</summary>
    [RelayCommand]
    private void AddProvider()
    {
        IsDropdownOpen = false;
        Navigate("Settings");
    }

    [RelayCommand]
    private void ResumeSession(string sessionId)
    {
        SelectedNavItem = "Chat";
        SessionResumeRequested?.Invoke(this, sessionId);
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(string sessionId)
    {
        await _sessionStore.DeleteAsync(sessionId, ownerUserId: App.SovrantUserId);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var item = RecentSessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (item is not null)
                RecentSessions.Remove(item);
        });
    }

    [RelayCommand]
    private async Task RefreshSessionsAsync()
    {
        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var sessions = await _sessionStore.ListWithTitlesAsync(ownerUserId: App.SovrantUserId);
        var items = new List<SessionListItem>();

        foreach (var s in sessions.Take(20))
        {
            var label = s.Title ?? s.SessionId;
            if (label.Length > 40)
                label = string.Concat(label.AsSpan(0, 37), "...");

            items.Add(new SessionListItem
            {
                SessionId = s.SessionId,
                Label = label,
                Timestamp = s.UpdatedAt,
                MessageCount = 0,
            });
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RecentSessions.Clear();
            foreach (var item in items.OrderByDescending(s => s.Timestamp))
                RecentSessions.Add(item);
        });
    }

}

public sealed class ProviderProfileEntry
{
    public string ProfileId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Persisted as TEXT in SQLite")]
    public string BaseUrl { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 32000;
    public bool IsWorkspaceProfile { get; set; }

    public override string ToString() => Name;
}

public partial class ProviderTreeGroup : ViewModelBase
{
    [ObservableProperty]
    private string _provider = string.Empty;

    public string ProfileId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Persisted as TEXT in SQLite")]
    public string BaseUrl { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 32000;

    [ObservableProperty]
    private int _modelCount;

    [ObservableProperty]
    private bool _isCurrent;

    public ObservableCollection<ModelOption> Models { get; } = [];

    /// <summary>True once we've successfully fetched the live model list for this provider in this session.</summary>
    public bool ModelsFetchedLive { get; set; }

    public bool IsWorkspaceProfile { get; set; }

    public override string ToString() => Provider;
}

public sealed class ModelOption(string model, ProviderTreeGroup group, bool isFree = false)
{
    public string Model { get; } = model;
    public ProviderTreeGroup Group { get; } = group;
    public bool IsFree { get; } = isFree;

    /// <summary>Display name with provider prefix stripped and :free/:extended removed.</summary>
    public string DisplayName => SidebarViewModel.ShortenModelName(Model);

    public override string ToString() => DisplayName;
}

public partial class SessionListItem : ViewModelBase
{
    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _timestamp;

    [ObservableProperty]
    private int _messageCount;
}

