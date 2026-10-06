using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>Phase 140 — the reworked sign-in window still takes typed email and password.</summary>
public sealed class LoginWindowInputTests
{
    [AvaloniaFact]
    public void Typing_Into_Email_Then_Password_Reaches_The_ViewModel()
    {
        var vm = new LoginViewModel(null!, null!, null!);
        var window = new LoginWindow { DataContext = vm };
        window.Show();

        window.FindControl<TextBox>("EmailBox")!.Focus();
        window.KeyTextInput("sam@example.com");
        window.FindControl<TextBox>("PasswordBox")!.Focus();
        window.KeyTextInput("Member-Pass-1");

        Assert.Equal("sam@example.com", vm.Email);
        Assert.Equal("Member-Pass-1", vm.Password);
        window.Close();
    }
}
