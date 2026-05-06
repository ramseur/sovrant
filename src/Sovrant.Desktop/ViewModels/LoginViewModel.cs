using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const string StoredTokenKey = "sovrant.desktop.auth_token";

    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRegistrationOpen;

    public event Action<string, string>? LoginSucceeded; // (userId, role)

    public LoginViewModel(IIdentityService identity, ITokenService tokens, ICredentialStore credentialStore)
    {
        _identity = identity;
        _tokens = tokens;
        _credentialStore = credentialStore;
    }

    public async Task InitializeAsync()
    {
        IsRegistrationOpen = await _identity.IsRegistrationOpenAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _identity.LoginAsync(Email, Password).ConfigureAwait(true);
            if (!result.Success || result.Token is null)
            {
                ErrorMessage = result.Error ?? "Login failed.";
                return;
            }

            await _credentialStore.StoreAsync(StoredTokenKey, result.Token).ConfigureAwait(true);
            LoginSucceeded?.Invoke(result.UserId!, result.Role ?? "user");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return;
        }

        IsBusy = true;
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
                ErrorMessage = "Account created. An administrator must approve it before you can sign in.";
                return;
            }

            await _credentialStore.StoreAsync(StoredTokenKey, result.Token!).ConfigureAwait(true);
            LoginSucceeded?.Invoke(result.UserId!, "admin");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
