using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sovrant.Desktop.ViewModels;

public partial class MessageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _role = "user";

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isUser = true;

    public ObservableCollection<ToolUseViewModel> ToolUses { get; } = [];

    partial void OnRoleChanged(string value) => IsUser = value == "user";

    public void AppendText(string chunk)
    {
        Text += chunk;
    }

    public void AddToolUse(string toolName, string toolUseId)
    {
        ToolUses.Add(new ToolUseViewModel
        {
            ToolName = toolName,
            ToolUseId = toolUseId,
            Status = "Running...",
        });
    }

    public void UpdateToolResult(string toolUseId, string content, bool isError)
    {
        foreach (var tu in ToolUses)
        {
            if (tu.ToolUseId == toolUseId)
            {
                tu.Result = content;
                tu.IsError = isError;
                tu.Status = isError ? "Error" : "Done";
                break;
            }
        }
    }
}

public partial class ToolUseViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private string _toolUseId = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private bool _isError;
}
