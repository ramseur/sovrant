using CommunityToolkit.Mvvm.ComponentModel;

namespace Sovrant.Desktop.ViewModels;

public partial class AutomationsViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusMessage = string.Empty;
}
