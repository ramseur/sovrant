using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Commands;

namespace Sovrant.Desktop.ViewModels;

public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly IReadOnlyList<ISlashCommand> _allCommands;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private CommandItem? _selectedItem;

    public ObservableCollection<CommandItem> FilteredCommands { get; } = [];

    public event EventHandler<SlashCommandResult>? CommandExecuted;

    public CommandPaletteViewModel(IEnumerable<ISlashCommand> commands)
    {
        _allCommands = commands.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            SearchText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ExecuteAsync(CommandItem? item)
    {
        if (item is null) return;
        IsOpen = false;

        var cmd = _allCommands.FirstOrDefault(c =>
            c.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
        if (cmd is null) return;

        var result = await cmd.ExecuteAsync(string.Empty);
        CommandExecuted?.Invoke(this, result);
    }

    private void ApplyFilter()
    {
        FilteredCommands.Clear();
        var query = SearchText.Trim();

        foreach (var cmd in _allCommands)
        {
            if (query.Length > 0
                && !cmd.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !cmd.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !cmd.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredCommands.Add(new CommandItem
            {
                Name = cmd.Name,
                Description = cmd.Description,
                Category = cmd.Category,
            });
        }
    }
}

public partial class CommandItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;
}
