using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Agents.Teams;

namespace Sovrant.Desktop.ViewModels;

public partial class MultiAgentViewModel : ViewModelBase
{
    private readonly ITeamRegistry _teamRegistry;

    public ObservableCollection<TeamMemberItemViewModel> Members { get; } = [];

    public MultiAgentViewModel(ITeamRegistry teamRegistry)
    {
        _teamRegistry = teamRegistry;
        LoadMembers();
    }

    [RelayCommand]
    private void Refresh() => LoadMembers();

    private void LoadMembers()
    {
        Members.Clear();
        foreach (var m in _teamRegistry.GetAllMembers())
        {
            Members.Add(new TeamMemberItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Role = m.Role.ToString(),
                TemplateName = m.Template ?? string.Empty,
            });
        }
    }
}

public partial class TeamMemberItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _templateName = string.Empty;
}
