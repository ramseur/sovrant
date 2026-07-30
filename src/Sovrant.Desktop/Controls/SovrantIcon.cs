using Avalonia;
using Lucide.Avalonia;

namespace Sovrant.Desktop.Controls;

public sealed class SovrantIcon : LucideIcon
{
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<SovrantIcon, string?>(nameof(Name));

    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == NameProperty)
            Kind = Resolve(change.GetNewValue<string?>());
    }

    private static LucideIconKind Resolve(string? name) => name switch
    {
        "activity" => LucideIconKind.Activity,
        "agent" => LucideIconKind.Bot,
        "alert" => LucideIconKind.TriangleAlert,
        "artifacts" => LucideIconKind.FileText,
        "bot" => LucideIconKind.Bot,
        "brain" => LucideIconKind.Brain,
        "building" => LucideIconKind.Building2,
        "chart" => LucideIconKind.ChartNoAxesCombined,
        "check" => LucideIconKind.Check,
        "check-circle" => LucideIconKind.CircleCheck,
        "cloud" => LucideIconKind.Cloud,
        "code" => LucideIconKind.Code,
        "database" => LucideIconKind.Database,
        "database-zap" => LucideIconKind.DatabaseZap,
        "document" => LucideIconKind.FileText,
        "file" => LucideIconKind.File,
        "file-chart" => LucideIconKind.FileChartColumn,
        "file-spreadsheet" => LucideIconKind.FileSpreadsheet,
        "folder" => LucideIconKind.Folder,
        "globe" => LucideIconKind.Globe,
        "governance" => LucideIconKind.Shield,
        "image" => LucideIconKind.Image,
        "integrations" => LucideIconKind.Plug,
        "kanban" => LucideIconKind.Kanban,
        "key" => LucideIconKind.KeyRound,
        "knowledge" => LucideIconKind.BookOpen,
        "layers" => LucideIconKind.Layers,
        "link" => LucideIconKind.Link,
        "lock" => LucideIconKind.Lock,
        "unlock" => LucideIconKind.LockOpen,
        "logout" => LucideIconKind.LogOut,
        "memory" => LucideIconKind.BrainCircuit,
        "message" => LucideIconKind.MessageSquare,
        "monitor" => LucideIconKind.Monitor,
        "music" => LucideIconKind.Music,
        "network" => LucideIconKind.Network,
        "orchestration" => LucideIconKind.Workflow,
        "panel" => LucideIconKind.PanelsTopLeft,
        "paperclip" => LucideIconKind.Paperclip,
        "play" => LucideIconKind.Play,
        "project" => LucideIconKind.Folder,
        "refresh" => LucideIconKind.RefreshCw,
        "route" => LucideIconKind.Route,
        "search" => LucideIconKind.Search,
        "send" => LucideIconKind.Send,
        "settings" => LucideIconKind.Settings2,
        "shield" => LucideIconKind.Shield,
        "snowflake" => LucideIconKind.Snowflake,
        "sparkles" => LucideIconKind.Sparkles,
        "square" => LucideIconKind.Square,
        "swarm" => LucideIconKind.Network,
        "target" => LucideIconKind.Target,
        "team" => LucideIconKind.Users,
        "tools" => LucideIconKind.Wrench,
        "users" => LucideIconKind.Users,
        "waypoints" => LucideIconKind.Waypoints,
        "wind" => LucideIconKind.Wind,
        "workflow" => LucideIconKind.Workflow,
        "x" => LucideIconKind.X,
        "zap" => LucideIconKind.Zap,
        _ => LucideIconKind.Circle,
    };
}
