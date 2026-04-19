using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Templates;
using SovrantDocumentFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Desktop.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly ITemplateRegistry _registry;
    private readonly IDocumentGeneratorRegistry _generators;
    private readonly ActiveContextViewModel _activeContext;
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

    public DocumentsViewModel(
        ITemplateRegistry registry,
        IDocumentGeneratorRegistry generators,
        ActiveContextViewModel activeContext)
    {
        _registry = registry;
        _generators = generators;
        _activeContext = activeContext;
        LoadTemplates();
    }

    [RelayCommand]
    private void Refresh() => LoadTemplates();

    [RelayCommand]
    private void SelectTemplate(TemplateItemViewModel? item)
    {
        SelectedTemplate = item;
    }

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
                ? ArtifactScope.DefaultWorkspaceId
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
