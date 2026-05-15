using CommunityToolkit.Mvvm.ComponentModel;

namespace Sovrant.Desktop.ViewModels;

public partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty;

    public PlaceholderViewModel(string title, string icon, string description)
    {
        _title = title;
        _icon = icon;
        _description = description;
    }
}
