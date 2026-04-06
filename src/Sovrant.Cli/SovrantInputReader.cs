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

        Console.Write(AnsiSaveCursor);

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                if (needsRender)
                {
                    UpdateAutocomplete(buffer.ToString(), commandNames, ref acMatches, ref acIndex, ref acVisible);
                    prevBoxHeight = RenderInputBox(buffer.ToString(), acMatches, acIndex, acVisible, prevBoxHeight);
                    lastRenderTime = Environment.TickCount64;
                    needsRender = false;
                }

                Thread.Sleep(10);

                var now = Environment.TickCount64;
                if (now - lastRenderTime > IdleRedrawMs)
                {
                    prevBoxHeight = RenderInputBox(buffer.ToString(), acMatches, acIndex, acVisible, prevBoxHeight);
                    lastRenderTime = now;
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
        matches = commandNames
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        visible = matches.Length > 0;
        if (selectedIndex >= matches.Length)
            selectedIndex = matches.Length - 1;
    }

    /// <summary>
    /// Renders the input box with blue borders. The box grows upward as the user
    /// types more lines. Returns the total height of the rendered box (for cleanup).
    /// </summary>
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

        Console.Write(AnsiHideCursor);

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

        // Clear previous render area if it was taller
        var maxClear = Math.Max(prevHeight, totalHeight) + 2;
        var bottom = termHeight - 1;
        for (var i = 0; i < maxClear; i++)
        {
            var row = bottom - i;
            if (row < 0) break;
            Console.SetCursorPosition(0, row);
            Console.Write(new string(' ', width));
        }

        // Starting row for autocomplete (topmost element)
        var startRow = bottom - totalHeight + 1;

        // Draw autocomplete menu
        if (acVisible)
        {
            for (var i = 0; i < acMatches.Length; i++)
            {
                var row = startRow + i;
                if (row < 0) continue;
                Console.SetCursorPosition(0, row);

                var label = $"  /{acMatches[i]}";
                if (label.Length > width - 1)
                    label = label[..(width - 1)];

                if (i == acIndex)
                    Console.Write($"\x1b[7m\x1b[36m{label}{new string(' ', Math.Max(0, width - label.Length))}{AnsiReset}");
                else
                    Console.Write($"{AnsiDim}{label}{AnsiReset}{new string(' ', Math.Max(0, width - label.Length))}");
            }
        }

        // Draw top border: ╭───────────────────────────────────╮
        var boxTop = startRow + acLineCount;
        if (boxTop >= 0)
        {
            Console.SetCursorPosition(0, boxTop);
            Console.Write($"{AnsiBoldBlue}{TopLeft}{new string(HorzBar, width - 2)}{TopRight}{AnsiReset}");
        }

        // Draw input content lines
        var scrollStart = Math.Max(0, wrappedLines.Count - MaxVisibleLines);
        for (var i = 0; i < visibleInputLines; i++)
        {
            var row = boxTop + 1 + i;
            if (row < 0 || row > bottom) continue;
            Console.SetCursorPosition(0, row);

            var lineIdx = scrollStart + i;
            var lineText = lineIdx < wrappedLines.Count ? wrappedLines[lineIdx] : string.Empty;

            // Pad or truncate to fill inner width
            if (lineText.Length > innerWidth)
                lineText = lineText[..innerWidth];

            var padding = innerWidth - lineText.Length;

            if (i == 0 && wrappedLines.Count <= 1)
            {
                // Single-line: show prompt and hint
                const string prompt = "> ";
                const string hint = " Esc to cancel";
                var contentWidth = innerWidth - prompt.Length - hint.Length;
                if (contentWidth < 0) contentWidth = 0;

                var displayText = lineText;
                if (displayText.Length > contentWidth)
                    displayText = displayText[^contentWidth..];
                var contentPad = contentWidth - displayText.Length;

                Console.Write(
                    $"{AnsiBoldBlue}{Vert}{AnsiReset} {AnsiBoldCyan}{prompt}{AnsiReset}{displayText}" +
                    $"{new string(' ', Math.Max(0, contentPad))}{AnsiDim}{hint}{AnsiReset} {AnsiBoldBlue}{Vert}{AnsiReset}");
            }
            else
            {
                // Multi-line: just show content
                Console.Write(
                    $"{AnsiBoldBlue}{Vert}{AnsiReset} {lineText}{new string(' ', Math.Max(0, padding))} {AnsiBoldBlue}{Vert}{AnsiReset}");
            }
        }

        // Draw bottom border: ╰───────────────────────────────────╯
        var boxBottom = boxTop + visibleInputLines + 1;
        if (boxBottom >= 0 && boxBottom <= bottom)
        {
            Console.SetCursorPosition(0, boxBottom);
            Console.Write($"{AnsiBoldBlue}{BotLeft}{new string(HorzBar, width - 2)}{BotRight}{AnsiReset}");
        }

        // Position cursor inside the box at the end of the current text
        var cursorLineInBox = Math.Min(wrappedLines.Count, MaxVisibleLines) - 1;
        var lastWrappedLine = wrappedLines.Count > 0 ? wrappedLines[^1] : string.Empty;
        var cursorCol = 2; // after "│ "
        if (wrappedLines.Count <= 1)
            cursorCol += 2 + lastWrappedLine.Length; // after "│ > " + text
        else
            cursorCol += lastWrappedLine.Length;

        var cursorRow = boxTop + 1 + cursorLineInBox;
        if (cursorRow >= 0 && cursorRow <= bottom && cursorCol < width - 1)
            Console.SetCursorPosition(cursorCol, cursorRow);

        Console.Write(AnsiSolidCursor);
        Console.Write(AnsiShowCursor);

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

        Console.Write(AnsiHideCursor);

        var totalClear = boxHeight + autocompleteLines + 2;
        for (var i = 0; i < totalClear; i++)
        {
            var row = bottom - i;
            if (row < 0) break;
            Console.SetCursorPosition(0, row);
            Console.Write(new string(' ', width));
        }

        Console.Write(AnsiRestoreCursor);
        Console.Write(AnsiSolidCursor);
        Console.Write(AnsiShowCursor);
    }
}
