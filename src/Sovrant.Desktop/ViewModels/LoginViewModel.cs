using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Sign-in window. Phase 140 (from Rahul Singh's issue #27): on a server with no accounts the
/// window switches to first-run mode, where the first account created becomes the administrator;
/// success messages use the success style instead of the error line.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private const string StoredTokenKey = "sovrant.desktop.auth_token";

    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _infoMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyLabel = string.Empty;
    [ObservableProperty] private bool _isRegistrationOpen;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private bool _isApprovalRequired;

    public event Action<string, string, string>? LoginSucceeded; // (userId, role, email)

    public LoginViewModel(IIdentityService identity, ITokenService tokens, ICredentialStore credentialStore)
    {
        _identity = identity;
        _tokens = tokens;
        _credentialStore = credentialStore;
    }

    /// <summary>The normal sign-in / create-account controls (hidden on first run).</summary>
    public bool ShowSignIn => !IsFirstRun;

    /// <summary>"Create account" is offered only once an admin exists and registration is open.</summary>
    public bool ShowRegistrationSection => !IsFirstRun && IsRegistrationOpen;

    public string Subtitle => IsFirstRun ? "Welcome! Let's set up your server." : "Sign in to continue.";

    public string PasswordWatermark => IsFirstRun ? "Choose a password" : "Password";

    partial void OnIsFirstRunChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSignIn));
        OnPropertyChanged(nameof(ShowRegistrationSection));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(PasswordWatermark));
    }

    partial void OnIsRegistrationOpenChanged(bool value) => OnPropertyChanged(nameof(ShowRegistrationSection));

    public async Task InitializeAsync()
    {
        IsFirstRun = await _identity.IsFirstRunAsync().ConfigureAwait(true);
        IsRegistrationOpen = await _identity.IsRegistrationOpenAsync().ConfigureAwait(true);
        IsApprovalRequired = !IsFirstRun && IsRegistrationOpen
            && await _identity.IsApprovalRequiredAsync().ConfigureAwait(true);
    }

    /// <summary>Enter key: on first run there's nobody to sign in as yet, so it creates the administrator.</summary>
    public Task SubmitAsync() => IsFirstRun ? RegisterAsync() : LoginAsync();

    private bool ValidateInput()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!ValidateInput()) return;
        IsBusy = true;
        BusyLabel = "Signing you in…";
        try
        {
            var result = await _identity.LoginAsync(Email, Password).ConfigureAwait(true);
            if (!result.Success || result.Token is null)
            {
                ErrorMessage = result.Error ?? "Login failed.";
                return;
            }

            await _credentialStore.StoreAsync(StoredTokenKey, result.Token).ConfigureAwait(true);
            LoginSucceeded?.Invoke(result.UserId!, result.Role ?? "user", Email.Trim());
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (!ValidateInput()) return;
        IsBusy = true;
        BusyLabel = IsFirstRun ? "Creating your administrator account…" : "Creating your account…";
        try
        {
            var result = await _identity.RegisterAsync(Email, Password).ConfigureAwait(true);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Registration failed.";
                return;
            }

            if (result.IsPendingApproval)
            {
                InfoMessage = "Account created. An administrator must approve it before you can sign in.";
                return;
            }

            await _credentialStore.StoreAsync(StoredTokenKey, result.Token!).ConfigureAwait(true);
            // Honor the role the identity service assigned. Only the first registrant
            // is "admin"; subsequent registrants are "user" and must not inherit
            // admin powers just because they used the register form.
            LoginSucceeded?.Invoke(result.UserId!, result.Role ?? "user", Email.Trim());
        }
        finally
        {
            IsBusy = false;
        }
    }
}
