using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>
/// Custom input reader that supports a sticky bottom bar, paste detection,
/// and Escape key handling. Replaces <see cref="Console.ReadLine"/>.
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

    private const string AnsiHideCursor = "\x1b[?25l";
    private const string AnsiShowCursor = "\x1b[?25h";
    private const string AnsiSolidCursor = "\x1b[2 q";  // Steady block cursor.
    private const string AnsiDefaultCursor = "\x1b[0 q"; // Restore terminal default cursor.

    // Saved output cursor row — where normal output should continue after the input bar is cleared.
    private static int s_savedOutputRow;

    /// <summary>
    /// Reads a line of input from the user with a sticky bottom bar.
    /// Falls back to <see cref="Console.ReadLine"/> when stdin is redirected.
    /// </summary>
    internal static InputResult ReadLine(CancellationToken ct = default)
    {
        if (Console.IsInputRedirected)
            return ReadLineFallback();

        return ReadLineInteractive(ct);
    }

    private static InputResult ReadLineFallback()
    {
        var line = Console.ReadLine();
        if (line is null)
            return new InputResult(string.Empty, true, false, 0);
        return new InputResult(line, false, false, 0);
    }

    private static InputResult ReadLineInteractive(CancellationToken ct)
    {
        var buffer = new System.Text.StringBuilder();
        var lineCount = 1;
        var lastKeyTime = Environment.TickCount64;
        var lastRenderTime = Environment.TickCount64;
        var isPaste = false;
        var needsRender = true;

        // Save current cursor row before we start drawing the input bar.
        s_savedOutputRow = Console.CursorTop;

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                // Render once after all buffered keys are drained.
                if (needsRender)
                {
                    RenderInputBar(buffer.ToString());
                    lastRenderTime = Environment.TickCount64;
                    needsRender = false;
                }

                Thread.Sleep(10);

                // Periodic redraw for terminal resize.
                var now = Environment.TickCount64;
                if (now - lastRenderTime > IdleRedrawMs)
                {
                    RenderInputBar(buffer.ToString());
                    lastRenderTime = now;
                }

                continue;
            }

            var key = Console.ReadKey(intercept: true);
            var keyTime = Environment.TickCount64;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearInputBar();
                    if (isPaste)
                        AnsiConsole.MarkupLine($"[grey][[Pasted {lineCount} lines]][/]");
                    return new InputResult(buffer.ToString(), false, isPaste, lineCount);

                case ConsoleKey.Escape:
                    ClearInputBar();
                    return new InputResult(string.Empty, true, false, 0);

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

        ClearInputBar();
        return new InputResult(string.Empty, true, false, 0);
    }

    private static void RenderInputBar(string currentText)
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
            return; // Terminal not available.
        }

        if (width < 20 || bottom < 2)
            return;

        // Hide cursor during redraw to prevent flicker.
        Console.Write(AnsiHideCursor);

        // Draw horizontal rule on the second-to-last line.
        Console.SetCursorPosition(0, bottom - 1);
        Console.Write(new string('\u2500', width));

        // Draw input bar on the last line.
        Console.SetCursorPosition(0, bottom);

        const string label = "> ";
        const string hint = "  Esc to cancel";
        var maxInput = width - label.Length - hint.Length - 1;

        // Show only the tail of the input if it's too long.
        var displayLine = currentText.Contains('\n', StringComparison.Ordinal)
            ? currentText[(currentText.LastIndexOf('\n') + 1)..]
            : currentText;
        var display = displayLine.Length > maxInput && maxInput > 0
            ? displayLine[^maxInput..]
            : displayLine;

        // Bold cyan label + text + padding + muted hint.
        Console.Write($"\x1b[1;36m{label}\x1b[0m{display}");

        var remaining = width - label.Length - display.Length - hint.Length;
        if (remaining > 0)
            Console.Write(new string(' ', remaining));

        Console.Write($"\x1b[2m{hint}\x1b[0m");

        // Place cursor right after the typed text on the input bar line.
        Console.SetCursorPosition(label.Length + display.Length, bottom);

        // Show a solid (non-blinking) block cursor.
        Console.Write(AnsiSolidCursor);
        Console.Write(AnsiShowCursor);
    }

    private static void ClearInputBar()
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
        Console.SetCursorPosition(0, bottom - 1);
        Console.Write(new string(' ', width));
        Console.SetCursorPosition(0, bottom);
        Console.Write(new string(' ', width));

        // Restore cursor to where output was before the input bar was drawn.
        // Clamp to avoid landing on the input bar area.
        var restoreRow = Math.Min(s_savedOutputRow, bottom - 2);
        Console.SetCursorPosition(0, restoreRow);
        Console.Write(AnsiDefaultCursor);
        Console.Write(AnsiShowCursor);
    }
}
