using System.Globalization;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>
/// Inline input reader with history, cursor movement, paste detection,
/// autocomplete, and Escape handling. No box drawing or absolute positioning —
/// content flows naturally like a standard terminal.
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

    private const int PasteThresholdMs = 50;
    private const int MaxHistorySize = 100;

    private const string AnsiDim = "\x1b[2m";
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiBoldCyan = "\x1b[1;36m";
    private const string AnsiClearLine = "\x1b[2K\r";

    private static readonly List<string> s_history = [];

    private static void AddHistory(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (s_history.Count > 0 && s_history[^1] == text) return;
        s_history.Add(text);
        if (s_history.Count > MaxHistorySize)
            s_history.RemoveAt(0);
    }

    /// <summary>
    /// Reads a line of input from the user with an inline prompt.
    /// Falls back to <see cref="Console.ReadLine"/> when stdin is redirected.
    /// </summary>
    internal static InputResult ReadLine(IReadOnlyList<string>? commandNames = null, CancellationToken ct = default)
    {
        if (Console.IsInputRedirected)
        {
            var line = Console.ReadLine();
            return line is null
                ? new InputResult(string.Empty, true, false, 0)
                : new InputResult(line, false, false, 0);
        }

        return ReadLineInteractive(commandNames, ct);
    }

    private static InputResult ReadLineInteractive(IReadOnlyList<string>? commandNames, CancellationToken ct)
    {
        var buffer = new System.Text.StringBuilder();
        var cursorPos = 0;
        var lineCount = 1;
        var lastKeyTime = Environment.TickCount64;
        var isPaste = false;

        // Autocomplete state
        var acIndex = -1;
        var acMatches = Array.Empty<string>();
        var acVisible = false;
        var prevAcCount = 0;

        // History state
        var historyPos = s_history.Count;
        var savedCurrentInput = string.Empty;

        // Drain stale keys from previous operations
        while (Console.KeyAvailable)
        {
            try { Console.ReadKey(intercept: true); }
            catch (InvalidOperationException) { break; }
        }

        // Render the initial prompt
        RenderPrompt(buffer.ToString(), cursorPos, acMatches, acIndex, acVisible, ref prevAcCount);

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(10);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            var keyTime = Environment.TickCount64;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    // Clear autocomplete if visible
                    if (acVisible)
                        ClearAutocomplete(prevAcCount);

                    // Move past the prompt line
                    Console.WriteLine();

                    var text = buffer.ToString();
                    AddHistory(text);

                    var wasPaste = isPaste && lineCount > 1;
                    if (wasPaste)
                        AnsiConsole.MarkupLine($"[grey][[Pasted {lineCount} lines]][/]");
                    return new InputResult(text, false, wasPaste, lineCount);

                case ConsoleKey.Tab:
                    if (acVisible && acMatches.Length > 0)
                    {
                        var sel = acIndex >= 0 ? acIndex : 0;
                        buffer.Clear();
                        buffer.Append('/').Append(acMatches[sel]);
                        cursorPos = buffer.Length;
                        acVisible = false;
                        acIndex = -1;
                        acMatches = [];
                    }
                    break;

                case ConsoleKey.Escape:
                    if (acVisible)
                    {
                        ClearAutocomplete(prevAcCount);
                        acVisible = false;
                        acIndex = -1;
                        acMatches = [];
                    }
                    else
                    {
                        Console.WriteLine();
                        return new InputResult(string.Empty, true, false, 0);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0) cursorPos--;
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Length) cursorPos++;
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Length;
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Length)
                    {
                        if (buffer[cursorPos] == '\n')
                            lineCount = Math.Max(1, lineCount - 1);
                        buffer.Remove(cursorPos, 1);
                    }
                    break;

                case ConsoleKey.UpArrow:
                    if (acVisible && acMatches.Length > 0)
                    {
                        acIndex = acIndex <= 0 ? acMatches.Length - 1 : acIndex - 1;
                    }
                    else if (s_history.Count > 0 && historyPos > 0)
                    {
                        if (historyPos == s_history.Count)
                            savedCurrentInput = buffer.ToString();
                        historyPos--;
                        buffer.Clear();
                        buffer.Append(s_history[historyPos]);
                        cursorPos = buffer.Length;
                        lineCount = 1;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (acVisible && acMatches.Length > 0)
                    {
                        acIndex = acIndex < acMatches.Length - 1 ? acIndex + 1 : 0;
                    }
                    else if (historyPos < s_history.Count)
                    {
                        historyPos++;
                        buffer.Clear();
                        buffer.Append(historyPos < s_history.Count
                            ? s_history[historyPos]
                            : savedCurrentInput);
                        cursorPos = buffer.Length;
                        lineCount = 1;
                    }
                    break;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        if (buffer[cursorPos - 1] == '\n')
                            lineCount = Math.Max(1, lineCount - 1);
                        buffer.Remove(cursorPos - 1, 1);
                        cursorPos--;
                    }
                    break;

                default:
                    if (key.KeyChar is '\n' or '\r')
                    {
                        buffer.Insert(cursorPos, '\n');
                        cursorPos++;
                        lineCount++;
                        if (keyTime - lastKeyTime < PasteThresholdMs)
                            isPaste = true;
                    }
                    else if (key.KeyChar != '\0')
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                    }
                    break;
            }

            lastKeyTime = keyTime;

            // Update autocomplete
            UpdateAutocomplete(buffer.ToString(), commandNames, ref acMatches, ref acIndex, ref acVisible);

            // Re-render the prompt line
            RenderPrompt(buffer.ToString(), cursorPos, acMatches, acIndex, acVisible, ref prevAcCount);
        }

        Console.WriteLine();
        return new InputResult(string.Empty, true, false, 0);
    }

    /// <summary>
    /// Renders the inline prompt on the current line. Overwrites the line in place
    /// using carriage return — no absolute cursor positioning needed.
    /// </summary>
    private static void RenderPrompt(
        string text, int cursorPos,
        string[] acMatches, int acIndex, bool acVisible,
        ref int prevAcCount)
    {
        int width;
        try { width = Console.WindowWidth; }
        catch (IOException) { width = 80; }

        const string prompt = "> ";
        var maxText = width - prompt.Length - 1;
        if (maxText < 1) maxText = 1;

        // For single-line display, take the line containing the cursor
        var displayText = text.Replace('\n', ' ');
        var displayCursorPos = cursorPos;

        // Scroll horizontally if text is too long
        var scrollOffset = 0;
        if (displayCursorPos > maxText)
        {
            scrollOffset = displayCursorPos - maxText + 10;
            if (scrollOffset < 0) scrollOffset = 0;
        }

        var visibleText = displayText.Length > scrollOffset
            ? displayText[scrollOffset..]
            : string.Empty;
        if (visibleText.Length > maxText)
            visibleText = visibleText[..maxText];

        var visualCursorCol = prompt.Length + (displayCursorPos - scrollOffset);

        // Handle autocomplete rendering above the prompt
        if (acVisible && acMatches.Length > 0)
        {
            // If autocomplete count changed, clear old lines
            if (prevAcCount > acMatches.Length)
                ClearAutocomplete(prevAcCount);

            // Save cursor, move up to render autocomplete lines
            var fb = new System.Text.StringBuilder();
            fb.Append("\x1b[s"); // save cursor

            for (var i = acMatches.Length - 1; i >= 0; i--)
            {
                fb.Append(CultureInfo.InvariantCulture, $"\x1b[{acMatches.Length - i + 1}A"); // move up
                fb.Append('\r');

                var label = $"  /{acMatches[i]}";
                if (label.Length > width - 2)
                    label = label[..(width - 2)];

                if (i == acIndex)
                    fb.Append("\x1b[7m\x1b[36m").Append(label).Append("\x1b[K").Append(AnsiReset);
                else
                    fb.Append(AnsiDim).Append(label).Append("\x1b[K").Append(AnsiReset);

                fb.Append("\x1b[u"); // restore cursor
            }
            Console.Write(fb.ToString());
            prevAcCount = acMatches.Length;
        }
        else if (prevAcCount > 0)
        {
            ClearAutocomplete(prevAcCount);
            prevAcCount = 0;
        }

        // Render the prompt line
        Console.Write($"\r{AnsiBoldCyan}{prompt}{AnsiReset}{visibleText}\x1b[K");

        // Position cursor
        if (visualCursorCol >= 0 && visualCursorCol < width)
            Console.Write($"\r\x1b[{visualCursorCol + 1}G");
    }

    private static void ClearAutocomplete(int count)
    {
        if (count <= 0) return;
        var fb = new System.Text.StringBuilder();
        fb.Append("\x1b[s"); // save cursor
        for (var i = 0; i < count; i++)
        {
            fb.Append(CultureInfo.InvariantCulture, $"\x1b[{count - i + 1}A\r\x1b[K\x1b[u");
        }
        Console.Write(fb.ToString());
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
            .Take(8)
            .ToArray();

        visible = matches.Length > 0;
        if (selectedIndex >= matches.Length)
            selectedIndex = matches.Length - 1;
    }

    /// <summary>
    /// Renders a simple "Processing... Esc to cancel" hint on the current line.
    /// </summary>
    internal static void RenderProcessingBox()
    {
        if (Console.IsInputRedirected) return;
        Console.Write($"{AnsiDim}Processing... Esc to cancel{AnsiReset}");
    }

    /// <summary>Clears the processing hint.</summary>
    internal static void ClearProcessingBox()
    {
        if (Console.IsInputRedirected) return;
        Console.Write(AnsiClearLine);
    }
}
