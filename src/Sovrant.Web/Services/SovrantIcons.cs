using System.Globalization;
using System.Net;
using Blazicons;
using Sovrant.Api.Ui;

namespace Sovrant.Web.Services;

/// <summary>
/// Phase 136 — Web's map from the shared <see cref="IconNames"/> vocabulary to Lucide
/// glyphs (Blazicons.Lucide). Renders plain inline SVG so existing CSS that sizes
/// <c>svg</c> inside its container keeps working; strokes use <c>currentColor</c>.
/// Desktop's equivalent is <c>SovrantIconMap</c>; the two packages bundle different
/// Lucide releases, so a name can map to a differently named glyph per surface.
/// </summary>
internal static class SovrantIcons
{
    public const double DefaultStrokeWidth = 1.9;

    internal static IReadOnlyDictionary<string, SvgIcon> Map { get; } = new Dictionary<string, SvgIcon>(StringComparer.Ordinal)
    {
        // Navigation
        [IconNames.Dashboard] = Lucide.LayoutDashboard,
        [IconNames.Chat] = Lucide.MessageCircle,
        [IconNames.Knowledge] = Lucide.BookOpen,
        [IconNames.Agents] = Lucide.Bot,
        [IconNames.Projects] = Lucide.FolderKanban,
        [IconNames.Admin] = Lucide.Shield,
        [IconNames.Workflow] = Lucide.Workflow,
        [IconNames.Orchestration] = Lucide.Waypoints,
        [IconNames.Swarm] = Lucide.Network,
        [IconNames.Team] = Lucide.Users,
        [IconNames.Sequential] = Lucide.ListOrdered,
        [IconNames.Parallel] = Lucide.Columns3,
        [IconNames.Integrations] = Lucide.Plug,
        [IconNames.Workspace] = Lucide.Building,
        [IconNames.Model] = Lucide.Cpu,
        [IconNames.Trust] = Lucide.ShieldCheck,
        [IconNames.Diagnostics] = Lucide.Activity,
        [IconNames.RailCollapse] = Lucide.PanelLeftClose,
        [IconNames.RailExpand] = Lucide.PanelLeftOpen,
        // Actions
        [IconNames.Search] = Lucide.Search,
        [IconNames.Add] = Lucide.Plus,
        [IconNames.Close] = Lucide.X,
        [IconNames.More] = Lucide.Ellipsis,
        [IconNames.Rename] = Lucide.Pencil,
        [IconNames.Delete] = Lucide.Trash2,
        [IconNames.Send] = Lucide.Send,
        [IconNames.Attach] = Lucide.Paperclip,
        [IconNames.Allow] = Lucide.Check,
        [IconNames.AllowAll] = Lucide.CheckCheck,
        [IconNames.Deny] = Lucide.Ban,
        [IconNames.Expand] = Lucide.ChevronRight,
        [IconNames.Refresh] = Lucide.RefreshCw,
        [IconNames.Stop] = Lucide.Square,
        [IconNames.Dropdown] = Lucide.ChevronDown,
        [IconNames.Back] = Lucide.ChevronLeft,
        [IconNames.SortAscending] = Lucide.ChevronUp,
        [IconNames.SortDescending] = Lucide.ChevronDown,
        // Objects
        [IconNames.Folder] = Lucide.Folder,
        [IconNames.NewFolder] = Lucide.FolderPlus,
        [IconNames.Tool] = Lucide.Wrench,
        [IconNames.Package] = Lucide.Package,
        [IconNames.Image] = Lucide.Image,
        [IconNames.Automation] = Lucide.Zap,
        [IconNames.Brand] = Lucide.Zap,
        [IconNames.File] = Lucide.File,
        [IconNames.FilePdf] = Lucide.FileText,
        [IconNames.FileWord] = Lucide.FileType,
        [IconNames.FileSheet] = Lucide.FileSpreadsheet,
        [IconNames.FileSlides] = Lucide.Presentation,
        [IconNames.FileMarkdown] = Lucide.FileCode,
        // Status
        [IconNames.Private] = Lucide.Lock,
        [IconNames.Public] = Lucide.LockOpen,
        [IconNames.Done] = Lucide.CircleCheck,
        [IconNames.Failed] = Lucide.CircleX,
        [IconNames.Warning] = Lucide.TriangleAlert,
        // Brand fallbacks
        [IconNames.ProviderCloud] = Lucide.Cloud,
        [IconNames.ProviderLocal] = Lucide.Server,
        [IconNames.IntegrationAutomation] = Lucide.Workflow,
        [IconNames.IntegrationPlatform] = Lucide.Plug,
        [IconNames.IntegrationDatabase] = Lucide.Database,
        [IconNames.IntegrationSearch] = Lucide.Globe,
        [IconNames.IntegrationDxp] = Lucide.LayoutTemplate,
    };

    /// <summary>
    /// Inline SVG markup for <paramref name="name"/>. Unknown names render Lucide's
    /// <c>CircleQuestionMark</c> so a typo is visible rather than blank (tests keep the map complete).
    /// </summary>
    public static string Svg(string name, double strokeWidth = DefaultStrokeWidth, int? size = null, string? cssClass = null)
    {
        var icon = Map.TryGetValue(name, out var found) ? found : Lucide.CircleQuestionMark;
        var dims = size is int s ? string.Create(CultureInfo.InvariantCulture, $" width=\"{s}\" height=\"{s}\"") : string.Empty;
        var cls = string.IsNullOrEmpty(cssClass) ? string.Empty : $" class=\"{WebUtility.HtmlEncode(cssClass)}\"";
        var stroke = strokeWidth.ToString("0.##", CultureInfo.InvariantCulture);
        return $"<svg viewBox=\"{icon.ViewBox}\"{dims}{cls} fill=\"none\" stroke=\"currentColor\" stroke-width=\"{stroke}\" " +
               $"stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">{icon.Content}</svg>";
    }
}
