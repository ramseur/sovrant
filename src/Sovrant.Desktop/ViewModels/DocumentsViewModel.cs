using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Workspaces;
using SovrantDocumentFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Desktop.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly ITemplateRegistry _registry;
    private readonly IDocumentGeneratorRegistry _generators;
    private readonly ActiveContextViewModel _activeContext;
    private readonly IKnowledgeStore _store;
    private readonly IPrincipalAccessor _principal;
    private readonly Action<string>? _launchChat;
    private readonly List<TemplateItemViewModel> _allTemplates = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private TemplateItemViewModel? _selectedTemplate;

    [ObservableProperty]
    private string _jsonInput = string.Empty;

    [ObservableProperty]
    private string _selectedFormatKey = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _resultPath = string.Empty;

    [ObservableProperty]
    private Uri? _resultUrl;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private MarkdownEditorViewModel? _activeEditor;

    [ObservableProperty]
    private bool _canEdit;

    private string? _editingSlug;
    private string? _carriedFieldsJson;

    public bool IsAdmin => _principal.IsAdmin;

    public ObservableCollection<TemplateItemViewModel> FilteredTemplates { get; } = [];

    public ObservableCollection<FormatOption> FormatOptions { get; } =
    [
        new FormatOption { Key = string.Empty, Display = "(default)" },
        new FormatOption { Key = "markdown", Display = "Markdown" },
        new FormatOption { Key = "pdf", Display = "PDF" },
        new FormatOption { Key = "structured_pdf", Display = "Structured PDF" },
        new FormatOption { Key = "word", Display = "Word" },
        new FormatOption { Key = "excel", Display = "Excel" },
        new FormatOption { Key = "powerpoint", Display = "PowerPoint" },
    ];

    public bool CanLaunchChat => _launchChat is not null;

    public DocumentsViewModel(
        ITemplateRegistry registry,
        IDocumentGeneratorRegistry generators,
        ActiveContextViewModel activeContext,
        IKnowledgeStore store,
        IPrincipalAccessor principal,
        Action<string>? launchChat = null)
    {
        _registry = registry;
        _generators = generators;
        _activeContext = activeContext;
        _store = store;
        _principal = principal;
        _launchChat = launchChat;
        LoadTemplates();
    }

    [RelayCommand]
    private void Refresh() => LoadTemplates();

    [RelayCommand]
    private void SelectTemplate(TemplateItemViewModel? item)
    {
        if (IsEditing) CancelEdit();
        SelectedTemplate = item;
        // Document templates are system-wide (BuiltIn) knowledge: only admins may edit them.
        CanEdit = item is not null && _principal.IsAdmin;
        ChatToCreateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task EditTemplate()
    {
        if (SelectedTemplate is null || !_principal.IsAdmin) return;
        var slug = SelectedTemplate.Id;

        // Carry the existing field schema forward unchanged — the editor exposes the
        // Scriban body + metadata frontmatter, not the raw JSON validation schema.
        var page = await _store.GetEffectiveAsync("document-templates", slug).ConfigureAwait(true);
        if (page is null) return;
        _carriedFieldsJson = page.FieldsJson;

        BeginEdit(slug, ReconstructTemplateMarkdown(page), page.Name);
    }

    [RelayCommand]
    private async Task RevertTemplate()
    {
        if (SelectedTemplate is null || !SelectedTemplate.HasUserOverride || !_principal.IsAdmin) return;
        var slug = SelectedTemplate.Id;
        await _store.DeleteAsync("document-templates", slug, KnowledgeScope.Global).ConfigureAwait(true);
        ReloadAndReselect(slug);
    }

    private void BeginEdit(string slug, string source, string title)
    {
        DetachEditor();
        _editingSlug = slug;
        var vm = new MarkdownEditorViewModel();
        vm.Load(source, title);
        vm.Saved += OnEditorSaved;
        vm.Cancelled += OnEditorCancelled;
        ActiveEditor = vm;
        IsEditing = true;
    }

    private async void OnEditorSaved(object? sender, string source)
    {
        var slug = _editingSlug;
        if (string.IsNullOrWhiteSpace(slug) || !_principal.IsAdmin) return;

        var (name, description, industry, defaultFormat, filenameTemplate, body) = ParseTemplateMarkdown(source);
        var now = DateTimeOffset.UtcNow;

        try
        {
            await _store.UpsertAsync(new KnowledgePage(
                KnowledgeId:      $"doctpl_global_{slug}",
                Kind:             "document-templates",
                Slug:             slug,
                Name:             string.IsNullOrWhiteSpace(name) ? slug : name,
                Description:      description,
                Tier:             "User",
                Body:             body,
                WorkspaceId:      KnowledgeScope.Global,
                CreatedAt:        now,
                UpdatedAt:        now,
                Industry:         industry,
                DefaultFormat:    defaultFormat,
                FieldsJson:       _carriedFieldsJson,
                FilenameTemplate: string.IsNullOrWhiteSpace(filenameTemplate) ? null : filenameTemplate))
                .ConfigureAwait(true);

            // Reload list before dismissing editor so any reload failure keeps the editor open.
            ReloadAndReselect(slug);
            EndEdit();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save template: {ex.Message}";
        }
    }

    private void OnEditorCancelled(object? sender, EventArgs e) => EndEdit();

    private void CancelEdit() => EndEdit();

    private void EndEdit()
    {
        DetachEditor();
        IsEditing = false;
        _editingSlug = null;
        _carriedFieldsJson = null;
    }

    private void DetachEditor()
    {
        if (ActiveEditor is null) return;
        ActiveEditor.Saved -= OnEditorSaved;
        ActiveEditor.Cancelled -= OnEditorCancelled;
        ActiveEditor = null;
    }

    private void ReloadAndReselect(string slug)
    {
        _registry.Reload();
        LoadTemplates();
        SelectedTemplate = _allTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, slug, StringComparison.OrdinalIgnoreCase));
        CanEdit = SelectedTemplate is not null && _principal.IsAdmin;
    }

    private static string ReconstructTemplateMarkdown(KnowledgePage p)
    {
        var ic = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(string.Create(ic, $"name: {p.Name}"));
        if (!string.IsNullOrWhiteSpace(p.Description))
            sb.AppendLine(string.Create(ic, $"description: {p.Description}"));
        if (!string.IsNullOrWhiteSpace(p.Industry))
            sb.AppendLine(string.Create(ic, $"industry: {p.Industry}"));
        if (!string.IsNullOrWhiteSpace(p.DefaultFormat))
            sb.AppendLine(string.Create(ic, $"default_format: {p.DefaultFormat}"));
        if (!string.IsNullOrWhiteSpace(p.FilenameTemplate))
            sb.AppendLine(string.Create(ic, $"filename_template: {p.FilenameTemplate}"));
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(p.Body);
        return sb.ToString();
    }

    private static (string Name, string Description, string Industry, string DefaultFormat, string FilenameTemplate, string Body)
        ParseTemplateMarkdown(string markdown)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = markdown;

        if (markdown.StartsWith("---", StringComparison.Ordinal))
        {
            var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end >= 0)
            {
                var fm = markdown.Substring(3, end - 3);
                body = markdown[(end + 4)..].TrimStart('\r', '\n');
                foreach (var line in fm.Split('\n'))
                {
                    var colon = line.IndexOf(':', StringComparison.Ordinal);
                    if (colon < 0) continue;
                    meta[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
            }
        }

        meta.TryGetValue("name", out var name);
        meta.TryGetValue("description", out var description);
        meta.TryGetValue("industry", out var industry);
        meta.TryGetValue("default_format", out var defaultFormat);
        meta.TryGetValue("filename_template", out var filenameTemplate);

        return (name?.Trim() ?? string.Empty,
                description?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(industry) ? "general" : industry.Trim(),
                string.IsNullOrWhiteSpace(defaultFormat) ? "markdown" : defaultFormat.Trim(),
                filenameTemplate?.Trim() ?? string.Empty,
                body);
    }

    [RelayCommand(CanExecute = nameof(CanLaunchChatNow))]
    private void ChatToCreate()
    {
        if (SelectedTemplate is null || _launchChat is null) return;
        _launchChat($"I'd like to create a {SelectedTemplate.Name} document. Please help me fill in the required information.");
    }

    private bool CanLaunchChatNow() => SelectedTemplate is not null && _launchChat is not null;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SelectedTemplate is null) return;
        ErrorMessage = string.Empty;
        HasResult = false;
        ResultSummary = string.Empty;
        ResultPath = string.Empty;
        ResultUrl = null;
        IsBusy = true;
        try
        {
            if (!_registry.TryResolve(SelectedTemplate.Id, out var template) || template is null)
            {
                ErrorMessage = $"Template not found: {SelectedTemplate.Id}";
                return;
            }

            JsonDocument data;
            try
            {
                data = JsonDocument.Parse(string.IsNullOrWhiteSpace(JsonInput) ? "{}" : JsonInput);
            }
            catch (JsonException ex)
            {
                ErrorMessage = $"Invalid JSON: {ex.Message}";
                return;
            }

            TemplateRenderResult rendered;
            try
            {
                rendered = template.Render(data.RootElement);
            }
            catch (TemplateValidationException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
            finally
            {
                data.Dispose();
            }

            var format = rendered.Format;
            if (!string.IsNullOrWhiteSpace(SelectedFormatKey) && TryParseFormat(SelectedFormatKey, out var overridden))
                format = overridden;

            var workspaceId = string.IsNullOrWhiteSpace(_activeContext.ActiveWorkspaceId)
                ? WorkspaceIdentity.DefaultPersonalFor(App.SovrantUserId)
                : _activeContext.ActiveWorkspaceId;
            var projectId = string.IsNullOrWhiteSpace(_activeContext.ActiveProjectId)
                ? ArtifactScope.DefaultProjectId
                : _activeContext.ActiveProjectId;

            var scope = new ArtifactScope
            {
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                RunId = $"desktop-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 40),
            };

            var request = new DocumentRequest
            {
                Format = format,
                Body = rendered.Body,
                Title = rendered.Title ?? template.Name,
                FileName = rendered.DefaultFileName,
                Scope = scope,
            };

            var generator = _generators.Resolve(format);
            var result = await generator.GenerateAsync(request, CancellationToken.None).ConfigureAwait(true);

            ResultPath = result.RelativePath;
            ResultSummary = $"{result.SizeBytes:N0} bytes · {result.ContentType}";
            ResultUrl = result.AccessUrl;
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
        // Slugs that have a User-tier overlay at the global scope → eligible for "Revert".
        HashSet<string> overrideSlugs;
        try
        {
            var overlays = _store.GetAllAsync("document-templates", KnowledgeScope.Global)
                                 .GetAwaiter().GetResult();
            overrideSlugs = new HashSet<string>(overlays.Select(o => o.Slug), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            overrideSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        _allTemplates.Clear();
        foreach (var t in _registry.All
            .OrderBy(t => t.Industry, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase))
        {
            var fields = new List<TemplateFieldViewModel>();
            foreach (var f in t.Fields)
            {
                fields.Add(new TemplateFieldViewModel
                {
                    Name = f.Name,
                    TypeName = FieldTypeName(f.Type),
                    Required = f.Required,
                    Description = f.Description ?? string.Empty,
                    IsNested = false,
                });
                if (f.ItemFields is { Count: > 0 })
                {
                    foreach (var nf in f.ItemFields)
                    {
                        fields.Add(new TemplateFieldViewModel
                        {
                            Name = "  " + nf.Name,
                            TypeName = FieldTypeName(nf.Type),
                            Required = nf.Required,
                            Description = nf.Description ?? string.Empty,
                            IsNested = true,
                        });
                    }
                }
            }

            _allTemplates.Add(new TemplateItemViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Industry = t.Industry,
                Description = t.Description ?? string.Empty,
                DefaultFormat = t.DefaultFormat,
                DefaultFormatName = FormatName(t.DefaultFormat),
                Fields = fields,
                HasUserOverride = overrideSlugs.Contains(t.Id),
            });
        }

        TotalCount = _allTemplates.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTemplateChanged(TemplateItemViewModel? value)
    {
        JsonInput = string.Empty;
        SelectedFormatKey = string.Empty;
        ErrorMessage = string.Empty;
        HasResult = false;
        ResultPath = string.Empty;
        ResultSummary = string.Empty;
        ResultUrl = null;
        CanEdit = value is not null && _principal.IsAdmin;
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
                && !t.Industry.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !t.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredTemplates.Add(t);
        }
    }

    private static string FormatName(SovrantDocumentFormat format) => format switch
    {
        SovrantDocumentFormat.Markdown => "Markdown",
        SovrantDocumentFormat.Pdf => "PDF",
        SovrantDocumentFormat.StructuredPdf => "Structured PDF",
        SovrantDocumentFormat.Word => "Word",
        SovrantDocumentFormat.Excel => "Excel",
        SovrantDocumentFormat.PowerPoint => "PowerPoint",
        _ => format.ToString(),
    };

    private static string FieldTypeName(TemplateFieldType type) => type switch
    {
        TemplateFieldType.String => "string",
        TemplateFieldType.Text => "text",
        TemplateFieldType.Integer => "integer",
        TemplateFieldType.Decimal => "decimal",
        TemplateFieldType.Currency => "currency",
        TemplateFieldType.Date => "date",
        TemplateFieldType.Boolean => "boolean",
        TemplateFieldType.StringArray => "string[]",
        TemplateFieldType.ObjectArray => "object[]",
        _ => type.ToString(),
    };

    private static bool TryParseFormat(string raw, out SovrantDocumentFormat format)
    {
        switch (raw.Trim().ToUpperInvariant())
        {
            case "MARKDOWN":
            case "MD":
                format = SovrantDocumentFormat.Markdown; return true;
            case "PDF":
                format = SovrantDocumentFormat.Pdf; return true;
            case "STRUCTURED_PDF":
            case "SPDF":
                format = SovrantDocumentFormat.StructuredPdf; return true;
            case "WORD":
            case "DOCX":
                format = SovrantDocumentFormat.Word; return true;
            case "EXCEL":
            case "XLSX":
                format = SovrantDocumentFormat.Excel; return true;
            case "POWERPOINT":
            case "PPTX":
                format = SovrantDocumentFormat.PowerPoint; return true;
            default:
                format = default;
                return false;
        }
    }
}

public partial class TemplateItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _industry = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public SovrantDocumentFormat DefaultFormat { get; init; }

    [ObservableProperty]
    private string _defaultFormatName = string.Empty;

    public IReadOnlyList<TemplateFieldViewModel> Fields { get; init; } = [];

    public bool HasUserOverride { get; init; }
}

public sealed class TemplateFieldViewModel
{
    public string Name { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsNested { get; init; }
    public string RequiredDisplay => Required ? "Yes" : string.Empty;
}

public sealed class FormatOption
{
    public string Key { get; init; } = string.Empty;
    public string Display { get; init; } = string.Empty;
}
