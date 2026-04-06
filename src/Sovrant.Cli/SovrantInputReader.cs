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
    private const string AnsiSaveCursor = "\x1b[s";
    private const string AnsiRestoreCursor = "\x1b[u";
    private const string AnsiSolidCursor = "\x1b[2 q";

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

        // Save cursor position so we can restore after clearing the input bar.
        Console.Write(AnsiSaveCursor);

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                if (needsRender)
                {
                    RenderInputBar(buffer.ToString());
                    lastRenderTime = Environment.TickCount64;
                    needsRender = false;
                }

                Thread.Sleep(10);

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
            return;
        }

        if (width < 20 || bottom < 2)
            return;

        Console.Write(AnsiHideCursor);

        Console.SetCursorPosition(0, bottom - 1);
        Console.Write(new string('\u2500', width));

        Console.SetCursorPosition(0, bottom);

        const string label = "> ";
        const string hint = "  Esc to cancel";
        var maxInput = width - label.Length - hint.Length - 1;

        var displayLine = currentText.Contains('\n', StringComparison.Ordinal)
            ? currentText[(currentText.LastIndexOf('\n') + 1)..]
            : currentText;
        var display = displayLine.Length > maxInput && maxInput > 0
            ? displayLine[^maxInput..]
            : displayLine;

        Console.Write($"\x1b[1;36m{label}\x1b[0m{display}");

        var remaining = width - label.Length - display.Length - hint.Length;
        if (remaining > 0)
            Console.Write(new string(' ', remaining));

        Console.Write($"\x1b[2m{hint}\x1b[0m");

        Console.SetCursorPosition(label.Length + display.Length, bottom);

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

        // Restore cursor to the position saved before the input bar was drawn.
        Console.Write(AnsiRestoreCursor);
        Console.Write(AnsiSolidCursor);
        Console.Write(AnsiShowCursor);
    }
}
