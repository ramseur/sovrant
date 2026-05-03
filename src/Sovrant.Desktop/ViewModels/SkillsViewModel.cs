using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Desktop.Views.Dialogs;
using Sovrant.Tools.Skills;

namespace Sovrant.Desktop.ViewModels;

public partial class SkillsViewModel : ViewModelBase
{
    private readonly SkillRegistry _registry;
    private readonly List<SkillItemViewModel> _allSkills = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private SkillItemViewModel? _selectedSkill;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<SkillItemViewModel> FilteredSkills { get; } = [];

    public SkillsViewModel(SkillRegistry registry)
    {
        _registry = registry;
        LoadSkills();
    }

    private const string NewSkillTemplate = """
        ---
        name: untitled-skill
        description: Briefly describe what this skill does and when to use it.
        trigger: /untitled
        ---
        ## Steps
        1. ...
        2. ...
        """;

    [RelayCommand]
    private void Refresh() => LoadSkills();

    [RelayCommand]
    private void SelectSkill(SkillItemViewModel skill) => SelectedSkill = skill;

    [RelayCommand]
    private async Task NewSkill() => await OpenEditor(string.Empty, NewSkillTemplate, readOnly: false, title: "New skill").ConfigureAwait(false);

    [RelayCommand]
    private async Task EditSkill()
    {
        if (SelectedSkill is null) return;
        var src = _registry.TryGetSource(SelectedSkill.Name);
        if (src is null) return;
        var content = File.Exists(src.Path) ? await File.ReadAllTextAsync(src.Path).ConfigureAwait(true) : SelectedSkill.Body;
        var slug = Path.GetFileNameWithoutExtension(src.Path);
        var readOnly = src.Tier == SkillTier.BuiltIn;
        await OpenEditor(slug, content, readOnly, title: SelectedSkill.Name).ConfigureAwait(false);
    }

    [RelayCommand]
    private void DeleteSkill()
    {
        if (SelectedSkill is null) return;
        var src = _registry.TryGetSource(SelectedSkill.Name);
        if (src is null || src.Tier == SkillTier.BuiltIn) return;
        var slug = Path.GetFileNameWithoutExtension(src.Path);
        _registry.DeleteGlobal(slug);
        SelectedSkill = null;
        LoadSkills();
    }

    private async Task OpenEditor(string slug, string source, bool readOnly, string title)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;

        var vm = new MarkdownEditorViewModel();
        vm.Load(source, readOnly, title);
        var dialog = new TemplateEditorDialog(vm);
        var result = await dialog.ShowDialog<string?>(owner).ConfigureAwait(true);
        if (result is null) return;

        var resolvedSlug = ExtractSlug(result) ?? slug;
        if (string.IsNullOrWhiteSpace(resolvedSlug)) return;
        _registry.SaveGlobal(resolvedSlug, result);
        LoadSkills();
    }

    private static Window? GetOwnerWindow() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private static string? ExtractSlug(string markdown)
    {
        if (!markdown.StartsWith("---", StringComparison.Ordinal)) return null;
        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return null;
        var fm = markdown.Substring(3, end - 3);
        foreach (var line in fm.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var raw = trimmed.Substring(5).Trim().Trim('"', '\'');
                return Slugify(raw);
            }
        }
        return null;
    }

    private static string Slugify(string raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in raw)
        {
            var c = char.ToLower(ch, CultureInfo.InvariantCulture);
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private void LoadSkills()
    {
        _allSkills.Clear();
        foreach (var s in _registry.All.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var src = _registry.TryGetSource(s.Name);
            var item = new SkillItemViewModel
            {
                Name = s.Name,
                Description = s.Description,
                Trigger = string.IsNullOrEmpty(s.Trigger) ? "(none)" : s.Trigger,
                AgentCount = s.Agents.Count,
                ToolCount = s.Tools.Count,
                Agents = s.Agents,
                Tools = s.Tools,
                Body = s.Body,
                Tier = src?.Tier ?? SkillTier.BuiltIn,
            };
            item.Markdown = BuildSkillMarkdown(item);
            _allSkills.Add(item);
        }

        TotalCount = _allSkills.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedSkillChanged(SkillItemViewModel? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildSkillMarkdown(value);
    }

    private void ApplyFilter()
    {
        FilteredSkills.Clear();
        var query = SearchText.Trim();

        foreach (var skill in _allSkills)
        {
            if (query.Length > 0
                && !skill.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !skill.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredSkills.Add(skill);
        }
    }

    private static string BuildSkillMarkdown(SkillItemViewModel skill)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {skill.Name}");
        sb.AppendLine();
        sb.AppendLine(skill.Description);
        sb.AppendLine();

        if (skill.Trigger != "(none)")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Trigger:** {skill.Trigger}");
            sb.AppendLine();
        }

        if (skill.Agents.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Agents:** {string.Join(", ", skill.Agents)}");
            sb.AppendLine();
        }

        if (skill.Tools.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools:** {string.Join(", ", skill.Tools)}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(skill.Body))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Workflow");
            sb.AppendLine();
            sb.Append(skill.Body);
        }

        return sb.ToString();
    }
}

public partial class SkillItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _trigger = string.Empty;

    [ObservableProperty]
    private int _agentCount;

    [ObservableProperty]
    private int _toolCount;

    public IReadOnlyList<string> Agents { get; init; } = [];
    public IReadOnlyList<string> Tools { get; init; } = [];
    public string Body { get; init; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;

    [ObservableProperty]
    private SkillTier _tier;

    public bool IsUserAuthored => Tier != SkillTier.BuiltIn;
}
