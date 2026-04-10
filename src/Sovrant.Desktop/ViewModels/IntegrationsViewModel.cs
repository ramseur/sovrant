using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sovrant.Runtime.Config;

namespace Sovrant.Desktop.ViewModels;

public partial class IntegrationsViewModel : ViewModelBase
{
    public ObservableCollection<McpServerItem> Servers { get; } = [];

    public IntegrationsViewModel(SovrantConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var (name, server) in config.McpServers)
        {
            Servers.Add(new McpServerItem
            {
                Name = name,
                Command = server.Command,
                ArgsSummary = server.Args.Count > 0 ? string.Join(" ", server.Args) : "—",
                HasOAuth = server.OAuthConfig is not null,
            });
        }
    }
}

public partial class McpServerItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _argsSummary = string.Empty;

    [ObservableProperty]
    private bool _hasOAuth;
}
