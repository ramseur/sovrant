using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class CodeViewModel : ViewModelBase
{
    private readonly ProjectTemplateRegistry _registry;
    private readonly IArtifactStore _store;
    private readonly ActiveContextViewModel _activeContext;
    private readonly List<CodeTemplateItemViewModel> _allTemplates = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private CodeTemplateItemViewModel? _selectedTemplate;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    public ObservableCollection<CodeTemplateItemViewModel> FilteredTemplates { get; } = [];
    public ObservableCollection<string> ResultFiles { get; } = [];

    public CodeViewModel(
        ProjectTemplateRegistry registry,
        IArtifactStore store,
        ActiveContextViewModel activeContext)
    {
        _registry = registry;
        _store = store;
        _activeContext = activeContext;
        LoadTemplates();
    }

    [RelayCommand]
    private void Refresh() => LoadTemplates();

    [RelayCommand]
    private void SelectTemplate(CodeTemplateItemViewModel? item) => SelectedTemplate = item;

    [RelayCommand]
    private async Task ScaffoldAsync()
    {
        if (SelectedTemplate is null) return;
        var name = ProjectName.Trim();
        if (string.IsNullOrEmpty(name)) { ErrorMessage = "Project name is required."; return; }

        var template = _registry.TryGet(SelectedTemplate.Id);
        if (template is null) { ErrorMessage = $"Template not found: {SelectedTemplate.Id}"; return; }

        ErrorMessage = string.Empty;
        HasResult = false;
        ResultSummary = string.Empty;
        ResultFiles.Clear();
        IsBusy = true;

        try
        {
            var context = new ScaffoldContext(name);
            var files = template.Scaffold(context);

            var runId = $"desktop-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
            var scope = new ArtifactScope
            {
                WorkspaceId = string.IsNullOrEmpty(_activeContext.ActiveWorkspaceId)
                    ? WorkspaceIdentity.DefaultPersonalFor(App.SovrantUserId)
                    : _activeContext.ActiveWorkspaceId,
                ProjectId = string.IsNullOrEmpty(_activeContext.ActiveProjectId)
                    ? ArtifactScope.DefaultProjectId
                    : _activeContext.ActiveProjectId,
                RunId = runId,
            };

            var handle = await _store.CreateRunScopeAsync(scope, CancellationToken.None).ConfigureAwait(true);

            foreach (var file in files)
            {
                var path = $"{name}/{file.RelativePath}";
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                using var stream = new System.IO.MemoryStream(bytes);
                await _store.WriteAsync(handle, path, stream, "text/plain", CancellationToken.None).ConfigureAwait(true);
                ResultFiles.Add(path);
            }

            ResultSummary = $"Scaffolded {files.Count} files · run {runId}";
            HasResult = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadTemplates()
    {
        _allTemplates.Clear();
        foreach (var t in _registry.All
            .OrderBy(t => t.Language, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Kind, StringComparer.OrdinalIgnoreCase))
        {
            _allTemplates.Add(new CodeTemplateItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Language = t.Language,
                Kind = t.Kind,
                Description = t.Description,
                Parameters = t.Parameters,
            });
        }

        TotalCount = _allTemplates.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTemplateChanged(CodeTemplateItemViewModel? value)
    {
        ProjectName = string.Empty;
        ErrorMessage = string.Empty;
        HasResult = false;
        ResultSummary = string.Empty;
        ResultFiles.Clear();
    }

    private void ApplyFilter()
    {
        FilteredTemplates.Clear();
        var q = SearchText.Trim();
        foreach (var t in _allTemplates)
        {
            if (q.Length > 0
                && !t.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Language.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Kind.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredTemplates.Add(t);
        }
    }
}

public partial class CodeTemplateItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _language = string.Empty;
    [ObservableProperty] private string _kind = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    public IReadOnlyList<ScaffoldParameter> Parameters { get; init; } = [];
}
