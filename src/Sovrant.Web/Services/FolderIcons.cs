using Microsoft.AspNetCore.Components;

namespace Sovrant.Web.Services;

/// <summary>
/// Phase 133 — line icons for the conversation-folder UI. Same paths as
/// docs/design/web.html (I.folder, I.folderPlus, I.dots, I.pencil, I.trash, I.chev).
/// </summary>
internal static class FolderIcons
{
    private static MarkupString Svg(string paths, double stroke = 2) => new(
        $"<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"{stroke.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{paths}</svg>");

    private const string FolderPath = "<path d=\"M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z\"/>";

    public static readonly MarkupString Folder = Svg(FolderPath);
    public static readonly MarkupString FolderPlus = Svg(FolderPath + "<path d=\"M12 11v6M9 14h6\"/>");
    public static readonly MarkupString Dots = Svg("<circle cx=\"5\" cy=\"12\" r=\"1\"/><circle cx=\"12\" cy=\"12\" r=\"1\"/><circle cx=\"19\" cy=\"12\" r=\"1\"/>", 2.4);
    public static readonly MarkupString Pencil = Svg("<path d=\"M12 20h9\"/><path d=\"M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z\"/>");
    public static readonly MarkupString Trash = Svg("<path d=\"M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6\"/>");
    public static readonly MarkupString Chevron = Svg("<polyline points=\"9 18 15 12 9 6\"/>", 2.4);
    public static readonly MarkupString Chat = Svg("<path d=\"M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z\"/>");
}
