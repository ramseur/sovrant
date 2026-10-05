using Avalonia;
using Lucide.Avalonia;
using Sovrant.Api.Ui;

namespace Sovrant.Desktop.Controls;

/// <summary>
/// Phase 136 — one icon from the shared vocabulary (<see cref="IconNames"/>), drawn with
/// Lucide.Avalonia. Colour comes from the inherited <c>Foreground</c> (like text), so an
/// icon inside a button follows the button's foreground. Set <see cref="IconName"/>, not
/// <c>Kind</c>.
/// </summary>
public sealed class SovrantIcon : LucideIcon
{
    public static readonly StyledProperty<string?> IconNameProperty =
        AvaloniaProperty.Register<SovrantIcon, string?>(nameof(IconName));

    static SovrantIcon()
    {
        StrokeWidthProperty.OverrideDefaultValue<SovrantIcon>(SovrantIconMap.DefaultStrokeWidth);
        SizeProperty.OverrideDefaultValue<SovrantIcon>(16);
    }

    /// <summary>An <see cref="IconNames"/> value, e.g. <c>IconNames.Close</c>.</summary>
    public string? IconName
    {
        get => GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IconNameProperty)
            Kind = string.IsNullOrEmpty(IconName) ? null : SovrantIconMap.Resolve(IconName);
    }
}

/// <summary>
/// Phase 136 — Desktop's map from <see cref="IconNames"/> to Lucide.Avalonia glyphs. This
/// package bundles a newer Lucide release than Web's Blazicons.Lucide, so a few glyphs are
/// named differently here (delete: <c>Trash</c> vs Web's <c>Trash2</c>, the same drawing).
/// </summary>
public static class SovrantIconMap
{
    public const double DefaultStrokeWidth = 1.9;

    public static IReadOnlyDictionary<string, LucideIconKind> Map { get; } = new Dictionary<string, LucideIconKind>(StringComparer.Ordinal)
    {
        // Navigation
        [IconNames.Dashboard] = LucideIconKind.LayoutDashboard,
        [IconNames.Chat] = LucideIconKind.MessageCircle,
        [IconNames.Knowledge] = LucideIconKind.BookOpen,
        [IconNames.Agents] = LucideIconKind.Bot,
        [IconNames.Projects] = LucideIconKind.FolderKanban,
        [IconNames.Admin] = LucideIconKind.Shield,
        [IconNames.Workflow] = LucideIconKind.Workflow,
        [IconNames.Orchestration] = LucideIconKind.Waypoints,
        [IconNames.Swarm] = LucideIconKind.Network,
        [IconNames.Team] = LucideIconKind.Users,
        [IconNames.Sequential] = LucideIconKind.ListOrdered,
        [IconNames.Parallel] = LucideIconKind.Columns3,
        [IconNames.Integrations] = LucideIconKind.Plug,
        [IconNames.Workspace] = LucideIconKind.Building,
        [IconNames.Model] = LucideIconKind.Cpu,
        [IconNames.Trust] = LucideIconKind.ShieldCheck,
        [IconNames.Diagnostics] = LucideIconKind.Activity,
        [IconNames.RailCollapse] = LucideIconKind.PanelLeftClose,
        [IconNames.RailExpand] = LucideIconKind.PanelLeftOpen,
        // Actions
        [IconNames.Search] = LucideIconKind.Search,
        [IconNames.Add] = LucideIconKind.Plus,
        [IconNames.Close] = LucideIconKind.X,
        [IconNames.More] = LucideIconKind.Ellipsis,
        [IconNames.Rename] = LucideIconKind.Pencil,
        [IconNames.Delete] = LucideIconKind.Trash,
        [IconNames.Send] = LucideIconKind.Send,
        [IconNames.Attach] = LucideIconKind.Paperclip,
        [IconNames.Allow] = LucideIconKind.Check,
        [IconNames.AllowAll] = LucideIconKind.CheckCheck,
        [IconNames.Deny] = LucideIconKind.Ban,
        [IconNames.Expand] = LucideIconKind.ChevronRight,
        [IconNames.Refresh] = LucideIconKind.RefreshCw,
        [IconNames.Stop] = LucideIconKind.Square,
        [IconNames.Dropdown] = LucideIconKind.ChevronDown,
        [IconNames.Back] = LucideIconKind.ChevronLeft,
        [IconNames.SortAscending] = LucideIconKind.ChevronUp,
        [IconNames.SortDescending] = LucideIconKind.ChevronDown,
        // Objects
        [IconNames.Folder] = LucideIconKind.Folder,
        [IconNames.NewFolder] = LucideIconKind.FolderPlus,
        [IconNames.Tool] = LucideIconKind.Wrench,
        [IconNames.Package] = LucideIconKind.Package,
        [IconNames.Image] = LucideIconKind.Image,
        [IconNames.Automation] = LucideIconKind.Zap,
        [IconNames.Brand] = LucideIconKind.Zap,
        [IconNames.File] = LucideIconKind.File,
        [IconNames.FilePdf] = LucideIconKind.FileText,
        [IconNames.FileWord] = LucideIconKind.FileType,
        [IconNames.FileSheet] = LucideIconKind.FileSpreadsheet,
        [IconNames.FileSlides] = LucideIconKind.Presentation,
        [IconNames.FileMarkdown] = LucideIconKind.FileCode,
        // Status
        [IconNames.Private] = LucideIconKind.Lock,
        [IconNames.Public] = LucideIconKind.LockOpen,
        [IconNames.Done] = LucideIconKind.CircleCheck,
        [IconNames.Failed] = LucideIconKind.CircleX,
        [IconNames.Warning] = LucideIconKind.TriangleAlert,
        // Brand fallbacks
        [IconNames.ProviderCloud] = LucideIconKind.Cloud,
        [IconNames.ProviderLocal] = LucideIconKind.Server,
        [IconNames.IntegrationAutomation] = LucideIconKind.Workflow,
        [IconNames.IntegrationPlatform] = LucideIconKind.Plug,
        [IconNames.IntegrationDatabase] = LucideIconKind.Database,
        [IconNames.IntegrationSearch] = LucideIconKind.Globe,
        [IconNames.IntegrationDxp] = LucideIconKind.LayoutTemplate,
    };

    /// <summary>The glyph for <paramref name="name"/>; unknown names draw a visible question mark.</summary>
    public static LucideIconKind Resolve(string name) =>
        Map.TryGetValue(name, out var kind) ? kind : LucideIconKind.CircleQuestionMark;
}
