using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scriban;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Workspaces;
using Spectre.Console;
using SovrantDocumentFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Cli;

/// <summary>
/// Builds the <c>sovrant document</c> command tree: listing available
/// document templates, showing a single template's field schema, and
/// rendering a template with data from a JSON file into the artifact
/// store.
/// </summary>
internal static class DocumentCommand
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    public static Command Build(Func<ParseResult, ServiceProvider> buildServices)
    {
        var root = new Command("document", "Browse and render registered document templates.");
        root.Add(BuildList(buildServices));
        root.Add(BuildFields(buildServices));
        root.Add(BuildRender(buildServices));
        root.Add(BuildSuggest(buildServices));
        root.Add(BuildLint(buildServices));
        return root;
    }

    private static Command BuildSuggest(Func<ParseResult, ServiceProvider> buildServices)
    {
        var promptArg = new Argument<string>("prompt")
        { Description = "Natural-language request, e.g. \"draft me an NDA between Acme and Globex\"." };
        var industryOpt = new Option<string?>("--industry")
        { Description = "Narrow candidates to a specific industry before ranking." };
        var limitOpt = new Option<int>("--limit")
        { Description = "Maximum number of matches to return (default 5).", DefaultValueFactory = _ => 5 };
        var jsonOpt = new Option<bool>("--json")
        { Description = "Emit machine-readable JSON instead of a table." };

        var cmd = new Command("suggest", "Route a natural-language request to the best-matching template(s).");
        cmd.Add(promptArg);
        cmd.Add(industryOpt);
        cmd.Add(limitOpt);
        cmd.Add(jsonOpt);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await using var sp = buildServices(pr);
            var registry = sp.GetRequiredService<ITemplateRegistry>();

            var prompt = pr.GetValue(promptArg) ?? string.Empty;
            var industry = pr.GetValue(industryOpt);
            var limit = pr.GetValue(limitOpt);

            var candidates = string.IsNullOrWhiteSpace(industry)
                ? (IEnumerable<IDocumentTemplate>)registry.All
                : registry.Find(industry, null);

            var matches = TemplateMatcher.Rank(candidates, prompt, limit);

            if (pr.GetValue(jsonOpt))
            {
                var payload = matches.Select(m => new
                {
                    id = m.Template.Id,
                    name = m.Template.Name,
                    industry = m.Template.Industry,
                    score = m.Score,
                    matched_terms = m.MatchedTerms,
                    required_fields = m.Template.Fields.Where(f => f.Required).Select(f => f.Name).ToArray(),
                });
                Console.WriteLine(JsonSerializer.Serialize(payload, s_jsonOpts));
                return;
            }

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No templates matched that prompt.[/] Try [bold]sovrant document list[/] to browse.");
                return;
            }

            var table = new Table()
                .Title($"[bold]Top {matches.Count} match(es) for:[/] {Markup.Escape(prompt)}")
                .AddColumns("Score", "Id", "Name", "Required fields", "Matched terms");
            foreach (var m in matches)
            {
                var required = string.Join(", ", m.Template.Fields.Where(f => f.Required).Select(f => f.Name));
                table.AddRow(
                    m.Score.ToString(CultureInfo.InvariantCulture),
                    Markup.Escape(m.Template.Id),
                    Markup.Escape(m.Template.Name),
                    Markup.Escape(string.IsNullOrEmpty(required) ? "(none)" : required),
                    Markup.Escape(string.Join(", ", m.MatchedTerms)));
            }
            AnsiConsole.Write(table);
        });

        return cmd;
    }

    private static Command BuildList(Func<ParseResult, ServiceProvider> buildServices)
    {
        var industryOpt = new Option<string?>("--industry")
        { Description = "Filter by industry (e.g. healthcare, finance, legal)." };
        var searchOpt = new Option<string?>("--search")
        { Description = "Free-text search over id, name, and description." };
        var jsonOpt = new Option<bool>("--json")
        { Description = "Emit machine-readable JSON instead of a table." };

        var cmd = new Command("list", "List registered document templates.");
        cmd.Add(industryOpt);
        cmd.Add(searchOpt);
        cmd.Add(jsonOpt);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await using var sp = buildServices(pr);
            var registry = sp.GetRequiredService<ITemplateRegistry>();

            var industry = pr.GetValue(industryOpt);
            var search = pr.GetValue(searchOpt);
            var asJson = pr.GetValue(jsonOpt);

            var hits = registry
                .Find(string.IsNullOrWhiteSpace(industry) ? null : industry,
                      string.IsNullOrWhiteSpace(search) ? null : search)
                .OrderBy(t => t.Industry, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (asJson)
            {
                var payload = hits.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    industry = t.Industry,
                    description = t.Description,
                    default_format = FormatName(t.DefaultFormat),
                });
                Console.WriteLine(JsonSerializer.Serialize(payload, s_jsonOpts));
                return;
            }

            if (hits.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No templates matched.[/]");
                return;
            }

            var table = new Table()
                .Title($"[bold]{hits.Count} template(s)[/]")
                .AddColumns("Id", "Name", "Industry", "Format", "Description");
            foreach (var t in hits)
            {
                table.AddRow(
                    Markup.Escape(t.Id),
                    Markup.Escape(t.Name),
                    Markup.Escape(t.Industry),
                    Markup.Escape(FormatName(t.DefaultFormat)),
                    Markup.Escape(t.Description ?? string.Empty));
            }
            AnsiConsole.Write(table);
        });

        return cmd;
    }

    private static Command BuildFields(Func<ParseResult, ServiceProvider> buildServices)
    {
        var idArg = new Argument<string>("template-id")
        { Description = "Template id, e.g. 'finance/invoice' or 'healthcare/hipaa-authorization'." };
        var jsonOpt = new Option<bool>("--json")
        { Description = "Emit machine-readable JSON instead of a table." };

        var cmd = new Command("fields", "Show the field schema for a template.");
        cmd.Add(idArg);
        cmd.Add(jsonOpt);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await using var sp = buildServices(pr);
            var registry = sp.GetRequiredService<ITemplateRegistry>();
            var id = pr.GetValue(idArg) ?? string.Empty;

            if (!registry.TryResolve(id, out var template) || template is null)
            {
                AnsiConsole.MarkupLine($"[red]Unknown template '{Markup.Escape(id)}'. Run [bold]sovrant document list[/] to see available templates.[/]");
                Environment.ExitCode = 1;
                return;
            }

            if (pr.GetValue(jsonOpt))
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    id = template.Id,
                    name = template.Name,
                    industry = template.Industry,
                    description = template.Description,
                    default_format = FormatName(template.DefaultFormat),
                    fields = ProjectFields(template.Fields),
                }, s_jsonOpts));
                return;
            }

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(template.Name)}[/]  ({Markup.Escape(template.Id)})");
            AnsiConsole.MarkupLine($"Industry: {Markup.Escape(template.Industry)}  ·  Default format: {Markup.Escape(FormatName(template.DefaultFormat))}");
            if (!string.IsNullOrWhiteSpace(template.Description))
                AnsiConsole.MarkupLine(Markup.Escape(template.Description));
            AnsiConsole.WriteLine();

            RenderFieldsTable(template.Fields, indent: 0);
        });

        return cmd;
    }

    private static void RenderFieldsTable(IReadOnlyList<TemplateField> fields, int indent)
    {
        var table = new Table().AddColumns("Name", "Type", "Required", "Description");
        foreach (var f in fields)
        {
            table.AddRow(
                Markup.Escape(new string(' ', indent * 2) + f.Name),
                Markup.Escape(FieldTypeName(f.Type)),
                f.Required ? "[red]yes[/]" : "no",
                Markup.Escape(f.Description ?? string.Empty));
        }
        AnsiConsole.Write(table);

        foreach (var f in fields)
        {
            if (f.ItemFields is { Count: > 0 })
            {
                AnsiConsole.MarkupLine($"[grey]Nested fields of [bold]{Markup.Escape(f.Name)}[/][/]:");
                RenderFieldsTable(f.ItemFields, indent + 1);
            }
        }
    }

    private static Command BuildRender(Func<ParseResult, ServiceProvider> buildServices)
    {
        var idArg = new Argument<string>("template-id")
        { Description = "Template id, e.g. 'finance/invoice'." };
        var dataOpt = new Option<string>("--data")
        { Description = "Path to a JSON file whose contents satisfy the template's fields.", Required = true };
        var formatOpt = new Option<string?>("--format")
        { Description = "Override the template's default format (markdown, word, excel, pdf, structured_pdf, powerpoint)." };
        var outOpt = new Option<string?>("--output")
        { Description = "Copy the rendered artifact to this local path after generation." };
        var runOpt = new Option<string?>("--run-id")
        { Description = "Run id that scopes the artifact (default: generated)." };
        var workspaceOpt = new Option<string?>("--workspace")
        { Description = "Workspace id (default: personal)." };
        var projectOpt = new Option<string?>("--project")
        { Description = "Project id (default: default-project)." };
        var jsonOpt = new Option<bool>("--json")
        { Description = "Emit a machine-readable JSON summary of the result." };

        var cmd = new Command("render", "Render a template with JSON data into an artifact.");
        cmd.Add(idArg);
        cmd.Add(dataOpt);
        cmd.Add(formatOpt);
        cmd.Add(outOpt);
        cmd.Add(runOpt);
        cmd.Add(workspaceOpt);
        cmd.Add(projectOpt);
        cmd.Add(jsonOpt);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await using var sp = buildServices(pr);
            var registry = sp.GetRequiredService<ITemplateRegistry>();
            var generators = sp.GetRequiredService<IDocumentGeneratorRegistry>();
            var store = sp.GetRequiredService<IArtifactStore>();

            var id = pr.GetValue(idArg) ?? string.Empty;
            var dataPath = pr.GetValue(dataOpt) ?? string.Empty;
            var asJson = pr.GetValue(jsonOpt);

            if (!registry.TryResolve(id, out var template) || template is null)
            {
                Fail($"Unknown template '{id}'. Run 'sovrant document list' to see available templates.", asJson);
                return;
            }

            if (!File.Exists(dataPath))
            {
                Fail($"Data file not found: {dataPath}", asJson);
                return;
            }

            JsonDocument dataDoc;
            try
            {
                await using var fs = File.OpenRead(dataPath);
                dataDoc = await JsonDocument.ParseAsync(fs, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Fail($"Invalid JSON in {dataPath}: {ex.Message}", asJson);
                return;
            }

            TemplateRenderResult rendered;
            try
            {
                rendered = template.Render(dataDoc.RootElement);
            }
            catch (TemplateValidationException ex)
            {
                Fail(ex.Message, asJson);
                return;
            }
            finally
            {
                dataDoc.Dispose();
            }

            var format = rendered.Format;
            var formatOverride = pr.GetValue(formatOpt);
            if (!string.IsNullOrWhiteSpace(formatOverride))
            {
                if (!TryParseFormat(formatOverride, out var overridden))
                {
                    Fail($"Unknown format '{formatOverride}'.", asJson);
                    return;
                }
                format = overridden;
            }

            var scope = new ArtifactScope
            {
                WorkspaceId = pr.GetValue(workspaceOpt) ?? WorkspaceIdentity.DefaultPersonal(),
                ProjectId = pr.GetValue(projectOpt) ?? ArtifactScope.DefaultProjectId,
                RunId = pr.GetValue(runOpt) ?? $"cli-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 40),
            };

            var request = new DocumentRequest
            {
                Format = format,
                Body = rendered.Body,
                Title = rendered.Title ?? template.Name,
                FileName = rendered.DefaultFileName,
                Scope = scope,
            };

            DocumentResult result;
            try
            {
                var generator = generators.Resolve(format);
                result = await generator.GenerateAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
            {
                Fail(ex.Message, asJson);
                return;
            }

            var outputPath = pr.GetValue(outOpt);
            string? copiedTo = null;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                await using var src = await store.ReadAsync(result.Handle, result.RelativePath, ct).ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
                await using var dst = File.Create(outputPath);
                await src.CopyToAsync(dst, ct).ConfigureAwait(false);
                copiedTo = Path.GetFullPath(outputPath);
            }

            if (asJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "generated",
                    template_id = template.Id,
                    format = FormatName(format),
                    path = result.RelativePath,
                    content_type = result.ContentType,
                    size_bytes = result.SizeBytes,
                    access_url = result.AccessUrl?.ToString(),
                    run_id = scope.RunId,
                    workspace_id = scope.WorkspaceId,
                    project_id = scope.ProjectId,
                    copied_to = copiedTo,
                }, s_jsonOpts));
                return;
            }

            AnsiConsole.MarkupLine($"[green]Generated[/] [bold]{Markup.Escape(template.Id)}[/] ({Markup.Escape(FormatName(format))})");
            AnsiConsole.MarkupLine($"  Artifact: {Markup.Escape(result.RelativePath)}");
            AnsiConsole.MarkupLine($"  Size:     {result.SizeBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes");
            AnsiConsole.MarkupLine($"  Run:      {Markup.Escape(scope.RunId)}");
            if (copiedTo is not null)
                AnsiConsole.MarkupLine($"  Copied to: {Markup.Escape(copiedTo)}");
            if (result.AccessUrl is not null)
                AnsiConsole.MarkupLine($"  URL:      {Markup.Escape(result.AccessUrl.ToString())}");
        });

        return cmd;
    }

    private static Command BuildLint(Func<ParseResult, ServiceProvider> buildServices)
    {
        var idOpt = new Option<string?>("--id")
        { Description = "Lint a single template by id (e.g. 'legal/nda'). Omit to lint all DB-backed templates." };
        var jsonOpt = new Option<bool>("--json")
        { Description = "Emit machine-readable JSON instead of a table." };

        var cmd = new Command("lint", "Validate Scriban syntax and field schema for DB-backed document templates.");
        cmd.Add(idOpt);
        cmd.Add(jsonOpt);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await using var sp = buildServices(pr);
            var store = sp.GetRequiredService<IKnowledgeStore>();
            var idFilter = pr.GetValue(idOpt);
            var asJson = pr.GetValue(jsonOpt);

            var pages = await store.GetAllEffectiveAsync("document-templates", ct: ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(idFilter))
                pages = pages.Where(p => p.Slug.Equals(idFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (pages.Count == 0)
            {
                if (asJson)
                    Console.WriteLine(JsonSerializer.Serialize(new { status = "no_templates", message = "No DB-backed templates found." }, s_jsonOpts));
                else
                    AnsiConsole.MarkupLine("[yellow]No DB-backed templates found.[/]");
                return;
            }

            var results = new List<LintResult>();
            foreach (var page in pages)
            {
                var errors = new List<string>();

                // Scriban syntax check
                try
                {
                    var tpl = Template.Parse(page.Body ?? string.Empty);
                    if (tpl.HasErrors)
                        errors.AddRange(tpl.Messages.Select(m => $"Scriban: {m.Message}"));
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add($"Scriban parse exception: {ex.Message}");
                }

                // fields_json well-formed JSON check
                if (!string.IsNullOrWhiteSpace(page.FieldsJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(page.FieldsJson);
                        if (doc.RootElement.ValueKind != JsonValueKind.Array)
                            errors.Add("fields_json: expected a JSON array");
                    }
                    catch (JsonException ex)
                    {
                        errors.Add($"fields_json: {ex.Message}");
                    }
                }

                results.Add(new LintResult(page.Slug, page.Name, errors));
            }

            var passCount = results.Count(r => r.Errors.Count == 0);
            var failCount = results.Count - passCount;

            if (asJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    total = results.Count,
                    passed = passCount,
                    failed = failCount,
                    templates = results.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        status = r.Errors.Count == 0 ? "pass" : "fail",
                        errors = r.Errors,
                    }),
                }, s_jsonOpts));
            }
            else
            {
                var table = new Table()
                    .Title($"[bold]Document template lint — {passCount}/{results.Count} passed[/]")
                    .AddColumns("Id", "Name", "Status", "Errors");
                foreach (var r in results)
                {
                    var status = r.Errors.Count == 0 ? "[green]pass[/]" : "[red]fail[/]";
                    var errText = r.Errors.Count == 0 ? string.Empty : Markup.Escape(string.Join("; ", r.Errors));
                    table.AddRow(Markup.Escape(r.Id), Markup.Escape(r.Name), status, errText);
                }
                AnsiConsole.Write(table);
            }

            if (failCount > 0)
                Environment.ExitCode = 1;
        });

        return cmd;
    }

    private sealed record LintResult(string Id, string Name, List<string> Errors);

    private static void Fail(string message, bool asJson)
    {
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { status = "error", message }, s_jsonOpts));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
        Environment.ExitCode = 1;
    }

    private static object[] ProjectFields(IReadOnlyList<TemplateField> fields) =>
        fields.Select(f => (object)new
        {
            name = f.Name,
            type = FieldTypeName(f.Type),
            required = f.Required,
            description = f.Description,
            item_fields = f.ItemFields is { Count: > 0 } ? ProjectFields(f.ItemFields) : null,
        }).ToArray();

    private static string FormatName(SovrantDocumentFormat format) => format switch
    {
        SovrantDocumentFormat.Markdown => "markdown",
        SovrantDocumentFormat.Pdf => "pdf",
        SovrantDocumentFormat.StructuredPdf => "structured_pdf",
        SovrantDocumentFormat.Word => "word",
        SovrantDocumentFormat.Excel => "excel",
        SovrantDocumentFormat.PowerPoint => "powerpoint",
        _ => "unknown",
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
        TemplateFieldType.StringArray => "string_array",
        TemplateFieldType.ObjectArray => "object_array",
        _ => "unknown",
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
