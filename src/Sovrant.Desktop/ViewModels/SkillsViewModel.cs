using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private MarkdownEditorViewModel? _activeEditor;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _editingSlug;

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
    private void SelectSkill(SkillItemViewModel skill)
    {
        if (IsEditing) CancelEdit();
        SelectedSkill = skill;
    }

    [RelayCommand]
    private void NewSkill() => BeginEdit(slug: string.Empty, source: NewSkillTemplate, title: "New skill");

    [RelayCommand]
    private async Task EditSkill()
    {
        if (SelectedSkill is null) return;
        var src = _registry.TryGetSource(SelectedSkill.Name);
        var content = src is not null && File.Exists(src.Path)
            ? await File.ReadAllTextAsync(src.Path).ConfigureAwait(true)
            : SelectedSkill.Body;
        var slug = src is not null ? Path.GetFileNameWithoutExtension(src.Path) : string.Empty;
        BeginEdit(slug, content, SelectedSkill.Name);
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

    private void BeginEdit(string slug, string source, string title)
    {
        DetachEditor();
        EditingSlug = slug;
        var vm = new MarkdownEditorViewModel();
        vm.Load(source, title);
        vm.Saved += OnEditorSaved;
        vm.Cancelled += OnEditorCancelled;
        ActiveEditor = vm;
        IsEditing = true;
    }

    private void OnEditorSaved(object? sender, string source)
    {
        var resolvedSlug = ExtractSlug(source) ?? EditingSlug ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedSlug)) return;
        _registry.SaveGlobal(resolvedSlug, source);
        var savedName = ExtractName(source);
        EndEdit();
        LoadSkills();
        if (!string.IsNullOrEmpty(savedName))
        {
            SelectedSkill = _allSkills.FirstOrDefault(s =>
                string.Equals(s.Name, savedName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string? ExtractName(string markdown)
    {
        if (!markdown.StartsWith("---", StringComparison.Ordinal)) return null;
        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return null;
        var fm = markdown.Substring(3, end - 3);
        foreach (var line in fm.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring(5).Trim().Trim('"', '\'');
        }
        return null;
    }

    private void OnEditorCancelled(object? sender, EventArgs e) => EndEdit();

    private void CancelEdit() => EndEdit();

    private void EndEdit()
    {
        DetachEditor();
        IsEditing = false;
        EditingSlug = null;
    }

    private void DetachEditor()
    {
        if (ActiveEditor is null) return;
        ActiveEditor.Saved -= OnEditorSaved;
        ActiveEditor.Cancelled -= OnEditorCancelled;
        ActiveEditor = null;
    }

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
