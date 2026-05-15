using CommunityToolkit.Mvvm.ComponentModel;

namespace Sovrant.Desktop.ViewModels;

public partial class McpConnectionItem : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public McpConnectionItem(string name)
    {
        Name = name;
    }
}
