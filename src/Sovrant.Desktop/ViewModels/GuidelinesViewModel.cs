using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Desktop.ViewModels;

public partial class GuidelinesViewModel : ViewModelBase
{
    private readonly IKnowledgeStore _store;
    private readonly List<GuidelineItemViewModel> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private GuidelineItemViewModel? _selectedGuideline;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private string _editBody = string.Empty;

    private string? _editingSlug;

    public ObservableCollection<GuidelineItemViewModel> FilteredGuidelines { get; } = [];

    public GuidelinesViewModel(IKnowledgeStore store)
    {
        _store = store;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private void SelectGuideline(GuidelineItemViewModel item)
    {
        if (IsEditing) CancelEditGuideline();
        SelectedGuideline = item;
    }

    [RelayCommand]
    private void BeginEditGuideline()
    {
        if (SelectedGuideline is null) return;
        _editingSlug = SelectedGuideline.Slug;
        EditName = SelectedGuideline.Name;
        EditDescription = SelectedGuideline.Description;
        EditBody = SelectedGuideline.Body;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveGuideline()
    {
        var slug = _editingSlug;
        if (string.IsNullOrWhiteSpace(slug)) return;

        var now = DateTimeOffset.UtcNow;
        await _store.UpsertAsync(new KnowledgePage(
            KnowledgeId: $"guideline_{slug}",
            Kind: "guidelines",
            Slug: slug,
            Name: EditName,
            Description: EditDescription,
            Tier: "User",
            Body: EditBody,
            WorkspaceId: "",
            CreatedAt: now,
            UpdatedAt: now)).ConfigureAwait(true);

        IsEditing = false;
        _editingSlug = null;
        await LoadAsync();
        SelectedGuideline = _all.FirstOrDefault(g => g.Slug == slug);
    }

    [RelayCommand]
    private void CancelEditGuideline()
    {
        IsEditing = false;
        _editingSlug = null;
    }

    [RelayCommand]
    private async Task RevertGuideline()
    {
        if (SelectedGuideline is null || !SelectedGuideline.HasUserOverride) return;
        var slug = SelectedGuideline.Slug;
        await _store.DeleteAsync("guidelines", slug).ConfigureAwait(true);
        await LoadAsync();
        SelectedGuideline = _all.FirstOrDefault(g => g.Slug == slug);
    }

    private async Task LoadAsync()
    {
        var dbRows = await _store.GetAllAsync("guidelines").ConfigureAwait(true);
        var overridesBySlug = dbRows
            .Where(r => r.Tier == "User")
            .ToDictionary(r => r.Slug, StringComparer.OrdinalIgnoreCase);

        _all.Clear();
        foreach (var g in LanguageGuidelines.All.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            overridesBySlug.TryGetValue(g.Slug, out var dbRow);
            _all.Add(new GuidelineItemViewModel
            {
                Slug = g.Slug,
                Name = dbRow?.Name ?? g.Name,
                Description = dbRow?.Description ?? g.Description,
                Body = dbRow?.Body ?? g.Body.TrimStart(),
                HasUserOverride = dbRow is not null,
            });
        }

        ApplyFilter();

        if (SelectedGuideline is not null)
            SelectedGuideline = _all.FirstOrDefault(g => g.Slug == SelectedGuideline.Slug);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGuidelineChanged(GuidelineItemViewModel? value)
    {
        if (value is null) { DetailMarkdown = string.Empty; return; }
        DetailMarkdown = $"# {value.Name}\n\n_{value.Description}_\n\n---\n\n{value.Body}";
    }

    private void ApplyFilter()
    {
        FilteredGuidelines.Clear();
        var query = SearchText.Trim();
        foreach (var g in _all)
        {
            if (query.Length > 0
                && !g.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !g.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredGuidelines.Add(g);
        }
    }
}

public partial class GuidelineItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public string Slug { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool HasUserOverride { get; init; }
}
