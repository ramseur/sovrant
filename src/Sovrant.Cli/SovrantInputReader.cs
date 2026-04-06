using System.Globalization;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>
/// Custom input reader that supports a sticky bottom bar with blue borders,
/// paste detection, Escape key handling, multi-line growth, and slash command
/// autocomplete. Replaces <see cref="Console.ReadLine"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Globalization", "CA1303", Justification = "ANSI escape sequences are not localizable.")]
internal sealed class SovrantInputReader
{
    /// <summary>Result of reading a line of input.</summary>
    internal sealed record InputResult(
        string Text,
        bool WasCancelled,
        bool WasPaste,
        int PastedLineCount);

    /// <summary>Time window (ms) to consider consecutive chars as paste rather than typing.</summary>
    private const int PasteThresholdMs = 50;

    /// <summary>Idle redraw interval (ms) for terminal resize handling.</summary>
    private const int IdleRedrawMs = 500;

    /// <summary>Maximum input lines before scrolling within the box.</summary>
    private const int MaxVisibleLines = 8;

    private const string AnsiHideCursor = "\x1b[?25l";
    private const string AnsiShowCursor = "\x1b[?25h";
    private const string AnsiSaveCursor = "\x1b[s";
    private const string AnsiRestoreCursor = "\x1b[u";
    private const string AnsiSolidCursor = "\x1b[2 q";

    // Blue color for the input box borders (matches project teal/cyan scheme).
    private const string AnsiBoldBlue = "\x1b[1;34m";
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiDim = "\x1b[2m";
    private const string AnsiBoldCyan = "\x1b[1;36m";

    /// <summary>Returns ANSI CSI sequence to move the cursor to the given 0-based row and col.</summary>
    private static string CursorTo(int row, int col = 0) =>
        string.Create(CultureInfo.InvariantCulture, $"\x1b[{row + 1};{col + 1}H");

    // Box-drawing characters.
    private const char HorzBar = '\u2500';
    private const char TopLeft = '\u256d';
    private const char TopRight = '\u256e';
    private const char BotLeft = '\u2570';
    private const char BotRight = '\u256f';
    private const char Vert = '\u2502';

    /// <summary>
    /// Reads a line of input from the user with a sticky bottom bar.
    /// Falls back to <see cref="Console.ReadLine"/> when stdin is redirected.
    /// </summary>
    /// <param name="commandNames">Optional list of slash command names (without leading /) for autocomplete.</param>
    /// <param name="ct">Cancellation token.</param>
    internal static InputResult ReadLine(IReadOnlyList<string>? commandNames = null, CancellationToken ct = default)
    {
        if (Console.IsInputRedirected)
            return ReadLineFallback();

        return ReadLineInteractive(commandNames, ct);
    }

    private static InputResult ReadLineFallback()
    {
        var line = Console.ReadLine();
        if (line is null)
            return new InputResult(string.Empty, true, false, 0);
        return new InputResult(line, false, false, 0);
    }

    private static InputResult ReadLineInteractive(IReadOnlyList<string>? commandNames, CancellationToken ct)
    {
        var buffer = new System.Text.StringBuilder();
        var lineCount = 1;
        var lastKeyTime = Environment.TickCount64;
        var lastRenderTime = Environment.TickCount64;
        var isPaste = false;
        var needsRender = true;

        // Autocomplete state
        var acIndex = -1;
        var acMatches = Array.Empty<string>();
        var acVisible = false;
        var prevBoxHeight = 0; // track previous render height for cleanup

        // Track terminal size to detect resizes without constant redraws.
        var lastWidth = 0;
        var lastHeight = 0;

        Console.Write(AnsiSaveCursor);

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                if (needsRender)
                {
                    UpdateAutocomplete(buffer.ToString(), commandNames, ref acMatches, ref acIndex, ref acVisible);
                    prevBoxHeight = RenderInputBox(buffer.ToString(), acMatches, acIndex, acVisible, prevBoxHeight);
                    try { lastWidth = Console.WindowWidth; lastHeight = Console.WindowHeight; } catch (IOException) { }
                    lastRenderTime = Environment.TickCount64;
                    needsRender = false;
                }

                Thread.Sleep(10);

                // Only redraw on idle if the terminal was actually resized.
                var now = Environment.TickCount64;
                if (now - lastRenderTime > IdleRedrawMs)
                {
                    lastRenderTime = now;
                    try
                    {
                        var w = Console.WindowWidth;
                        var h = Console.WindowHeight;
                        if (w != lastWidth || h != lastHeight)
                        {
                            lastWidth = w;
                            lastHeight = h;
                            needsRender = true;
                        }
                    }
                    catch (IOException) { }
                }

                continue;
            }

            var key = Console.ReadKey(intercept: true);
            var keyTime = Environment.TickCount64;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearInputBox(prevBoxHeight, acVisible ? acMatches.Length : 0);
                    if (isPaste)
                        AnsiConsole.MarkupLine($"[grey][[Pasted {lineCount} lines]][/]");
                    return new InputResult(buffer.ToString(), false, isPaste, lineCount);

                case ConsoleKey.Tab:
                    if (acVisible && acMatches.Length > 0)
                    {
                        var sel = acIndex >= 0 ? acIndex : 0;
                        buffer.Clear();
                        buffer.Append('/').Append(acMatches[sel]);
                        acVisible = false;
                        acIndex = -1;
                        acMatches = [];
                        needsRender = true;
                    }
                    break;

                case ConsoleKey.Escape:
                    if (acVisible)
                    {
                        acVisible = false;
                        acIndex = -1;
                        acMatches = [];
                        needsRender = true;
                    }
                    else
                    {
                        ClearInputBox(prevBoxHeight, 0);
                        return new InputResult(string.Empty, true, false, 0);
                    }
                    break;

                case ConsoleKey.UpArrow:
                    if (acVisible && acMatches.Length > 0)
                    {
                        acIndex = acIndex <= 0 ? acMatches.Length - 1 : acIndex - 1;
                        needsRender = true;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (acVisible && acMatches.Length > 0)
                    {
                        acIndex = acIndex < acMatches.Length - 1 ? acIndex + 1 : 0;
                        needsRender = true;
                    }
                    break;

                case ConsoleKey.Backspace:
                    if (buffer.Length > 0)
                    {
                        if (buffer[^1] == '\n')
                            lineCount = Math.Max(1, lineCount - 1);
                        buffer.Remove(buffer.Length - 1, 1);
                        needsRender = true;
                    }
                    break;

                default:
                    if (key.KeyChar is '\n' or '\r')
                    {
                        buffer.Append('\n');
                        lineCount++;
                        if (keyTime - lastKeyTime < PasteThresholdMs)
                            isPaste = true;
                    }
                    else if (key.KeyChar != '\0')
                    {
                        buffer.Append(key.KeyChar);
                        if (keyTime - lastKeyTime < PasteThresholdMs && buffer.Length > 1)
                            isPaste = true;
                    }

                    needsRender = true;
                    break;
            }

            lastKeyTime = keyTime;
        }

        ClearInputBox(prevBoxHeight, acVisible ? acMatches.Length : 0);
        return new InputResult(string.Empty, true, false, 0);
    }

    private static void UpdateAutocomplete(
        string text,
        IReadOnlyList<string>? commandNames,
        ref string[] matches,
        ref int selectedIndex,
        ref bool visible)
    {
        if (commandNames is null or { Count: 0 }
            || !text.StartsWith('/')
            || text.Contains(' ', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal))
        {
            matches = [];
            selectedIndex = -1;
            visible = false;
            return;
        }

        var prefix = text[1..];

        // Don't show autocomplete until the user has typed at least one character
        // after the slash — avoids a huge menu listing every command on bare "/".
        if (prefix.Length == 0)
        {
            matches = [];
            selectedIndex = -1;
            visible = false;
            return;
        }

        matches = commandNames
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(8) // cap visible items to avoid overrunning the screen
            .ToArray();

        visible = matches.Length > 0;
        if (selectedIndex >= matches.Length)
            selectedIndex = matches.Length - 1;
    }

    /// <summary>
    /// Renders the input box with blue borders. The box grows upward as the user
    /// types more lines. Returns the total height of the rendered box (for cleanup).
    /// </summary>
    /// <remarks>
    /// The entire frame is built in a single <see cref="System.Text.StringBuilder"/>
    /// and flushed with one <see cref="Console.Write(string)"/> call to eliminate
    /// flicker caused by intermediate cursor-move + write pairs.
    /// </remarks>
    private static int RenderInputBox(
        string currentText, string[] acMatches, int acIndex, bool acVisible, int prevHeight)
    {
        int width;
        int termHeight;
        try
        {
            width = Console.WindowWidth;
            termHeight = Console.WindowHeight;
        }
        catch (IOException)
        {
            return prevHeight;
        }

        if (width < 20 || termHeight < 4)
            return prevHeight;

        // Calculate how many input lines we need to show.
        var innerWidth = width - 4; // "│ " + content + " │"
        var wrappedLines = WrapText(currentText, innerWidth);
        var visibleInputLines = Math.Min(wrappedLines.Count, MaxVisibleLines);
        if (visibleInputLines < 1) visibleInputLines = 1;

        // Box = top border (1) + input lines + bottom border (1) = visibleInputLines + 2
        var boxHeight = visibleInputLines + 2;

        // Autocomplete sits above the box
        var acLineCount = acVisible ? acMatches.Length : 0;
        var totalHeight = boxHeight + acLineCount;

        var bottom = termHeight - 1;
        var startRow = bottom - totalHeight + 1;

        // Hide cursor once, build the full frame, show cursor at the end.
        // This prevents any visible cursor jumping during the write.
        var fb = new System.Text.StringBuilder(width * (totalHeight + 4));
        fb.Append(AnsiHideCursor);

        // Only blank rows that are above the current render area (box shrank).
        // Content rows are overwritten in-place with full-width padding — no blank pass needed.
        if (prevHeight > totalHeight)
        {
            var prevStartRow = bottom - prevHeight + 1;
            for (var row = prevStartRow; row < startRow; row++)
            {
                if (row < 0) continue;
                fb.Append(CursorTo(row));
                fb.Append(new string(' ', width));
            }
        }

        // Autocomplete menu
        if (acVisible)
        {
            for (var i = 0; i < acMatches.Length; i++)
            {
                var row = startRow + i;
                if (row < 0) continue;
                fb.Append(CursorTo(row));

                var label = $"  /{acMatches[i]}";
                if (label.Length > width - 2)
                    label = label[..(width - 2)];

                // Write label then erase-to-EOL to clear old content without wrapping.
                if (i == acIndex)
                    fb.Append("\x1b[7m\x1b[36m").Append(label).Append("\x1b[K").Append(AnsiReset);
                else
                    fb.Append(AnsiDim).Append(label).Append(AnsiReset).Append("\x1b[K");
            }
        }

        // Top border: ╭───────╮
        var boxTop = startRow + acLineCount;
        if (boxTop >= 0)
        {
            fb.Append(CursorTo(boxTop));
            fb.Append(AnsiBoldBlue).Append(TopLeft).Append(new string(HorzBar, width - 2)).Append(TopRight).Append(AnsiReset);
        }

        // Input content lines — each line is padded to full inner width.
        var scrollStart = Math.Max(0, wrappedLines.Count - MaxVisibleLines);
        for (var i = 0; i < visibleInputLines; i++)
        {
            var row = boxTop + 1 + i;
            if (row < 0 || row > bottom) continue;
            fb.Append(CursorTo(row));

            var lineIdx = scrollStart + i;
            var lineText = lineIdx < wrappedLines.Count ? wrappedLines[lineIdx] : string.Empty;

            if (lineText.Length > innerWidth)
                lineText = lineText[..innerWidth];

            var padding = innerWidth - lineText.Length;

            if (i == 0 && wrappedLines.Count <= 1)
            {
                const string prompt = "> ";
                const string hint = " Esc to cancel";
                var contentWidth = innerWidth - prompt.Length - hint.Length;
                if (contentWidth < 0) contentWidth = 0;

                var displayText = lineText;
                if (displayText.Length > contentWidth)
                    displayText = displayText[^contentWidth..];
                var contentPad = contentWidth - displayText.Length;

                fb.Append(AnsiBoldBlue).Append(Vert).Append(AnsiReset)
                  .Append(' ').Append(AnsiBoldCyan).Append(prompt).Append(AnsiReset)
                  .Append(displayText)
                  .Append(new string(' ', Math.Max(0, contentPad)))
                  .Append(AnsiDim).Append(hint).Append(AnsiReset)
                  .Append(' ').Append(AnsiBoldBlue).Append(Vert).Append(AnsiReset);
            }
            else
            {
                fb.Append(AnsiBoldBlue).Append(Vert).Append(AnsiReset)
                  .Append(' ').Append(lineText)
                  .Append(new string(' ', Math.Max(0, padding)))
                  .Append(' ').Append(AnsiBoldBlue).Append(Vert).Append(AnsiReset);
            }
        }

        // Bottom border: ╰───────╯
        var boxBottom = boxTop + visibleInputLines + 1;
        if (boxBottom >= 0 && boxBottom <= bottom)
        {
            fb.Append(CursorTo(boxBottom));
            fb.Append(AnsiBoldBlue).Append(BotLeft).Append(new string(HorzBar, width - 2)).Append(BotRight).Append(AnsiReset);
        }

        // Position cursor inside the box, then make it visible again.
        var cursorLineInBox = Math.Min(wrappedLines.Count, MaxVisibleLines) - 1;
        var lastWrappedLine = wrappedLines.Count > 0 ? wrappedLines[^1] : string.Empty;
        var cursorCol = 2; // after "│ "
        if (wrappedLines.Count <= 1)
            cursorCol += 2 + lastWrappedLine.Length; // after "│ > " + text
        else
            cursorCol += lastWrappedLine.Length;

        var cursorRow = boxTop + 1 + cursorLineInBox;
        if (cursorRow >= 0 && cursorRow <= bottom && cursorCol < width - 1)
            fb.Append(CursorTo(cursorRow, cursorCol));

        fb.Append(AnsiShowCursor);

        // Single atomic write.
        Console.Write(fb.ToString());

        return totalHeight;
    }

    /// <summary>Wraps text to fit within the given width, splitting on word boundaries when possible.</summary>
    private static List<string> WrapText(string text, int maxWidth)
    {
        if (maxWidth <= 0) maxWidth = 1;

        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        // Split on explicit newlines first
        var rawLines = text.Split('\n');
        foreach (var raw in rawLines)
        {
            if (raw.Length <= maxWidth)
            {
                lines.Add(raw);
            }
            else
            {
                // Hard-wrap long lines
                for (var i = 0; i < raw.Length; i += maxWidth)
                {
                    var len = Math.Min(maxWidth, raw.Length - i);
                    lines.Add(raw.Substring(i, len));
                }
            }
        }

        return lines;
    }

    private static void ClearInputBox(int boxHeight, int autocompleteLines)
    {
        int width;
        int bottom;
        try
        {
            width = Console.WindowWidth;
            bottom = Console.WindowHeight - 1;
        }
        catch (IOException)
        {
            return;
        }

        if (bottom < 2)
            return;

        // Restore cursor to the position saved before the box was drawn,
        // then erase everything from there to the end of the screen.
        // This removes the box and any leftover blank rows in one shot.
        Console.Write($"{AnsiHideCursor}{AnsiRestoreCursor}\x1b[J{AnsiSolidCursor}{AnsiShowCursor}");
    }
}
