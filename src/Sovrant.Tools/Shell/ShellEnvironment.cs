using System.Runtime.InteropServices;

namespace Sovrant.Tools.Shell;

/// <summary>
/// Phase 43 — one-shot detection of the shell environment Sovrant will use:
/// the PowerShell executable + version on Windows, whether the current
/// process is running elevated (admin / root), and cached metadata the
/// rest of the tool layer can read without re-probing.
///
/// All detection is lazy and cached per-process. A background boot call
/// from DI warms the cache so the first BashTool invocation doesn't pay
/// the startup probe latency.
/// </summary>
public sealed class ShellEnvironment
{
    private readonly Lazy<PowerShellInfo?> _powerShell;
    private readonly Lazy<bool> _isElevated;

    public ShellEnvironment()
    {
        _powerShell = new Lazy<PowerShellInfo?>(DetectPowerShell, isThreadSafe: true);
        _isElevated = new Lazy<bool>(DetectElevation, isThreadSafe: true);
    }

    /// <summary>
    /// Information about the PowerShell host Sovrant will spawn on Windows.
    /// Null on non-Windows (Linux/macOS use /bin/bash and don't consult this).
    /// </summary>
    public PowerShellInfo? PowerShell => _powerShell.Value;

    /// <summary>
    /// True if the Sovrant process is running with elevated privileges
    /// (Administrator on Windows, root on Unix). Used to surface a warning
    /// in tool confirmations — shell commands run as admin have a wider
    /// blast radius than the user may expect.
    /// </summary>
    public bool IsElevated => _isElevated.Value;

    // ── PowerShell detection ─────────────────────────────────────────────

    private static PowerShellInfo? DetectPowerShell()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var path = FindPowerShellExecutable();
        if (path is null) return null;

        // Probe version by running `$PSVersionTable.PSVersion.ToString();$PSVersionTable.PSEdition`.
        // Keep this dead cheap: -NoProfile, short timeout, no script file.
        // If it fails, return a best-effort record with just the path so
        // BashTool can still spawn it — we just lose the version hint.
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("$PSVersionTable.PSVersion.ToString() + '|' + $PSVersionTable.PSEdition");

            using var proc = System.Diagnostics.Process.Start(psi)!;
            if (!proc.WaitForExit(5000))
            {
                try { proc.Kill(); } catch (InvalidOperationException) { }
                return new PowerShellInfo(path, Version: null, Edition: null);
            }

            var stdout = proc.StandardOutput.ReadToEnd().Trim();
            var pipe = stdout.IndexOf('|', StringComparison.Ordinal);
            if (pipe < 0) return new PowerShellInfo(path, Version: stdout, Edition: null);

            var version = stdout[..pipe].Trim();
            var edition = stdout[(pipe + 1)..].Trim();
            return new PowerShellInfo(path, version, edition);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new PowerShellInfo(path, Version: null, Edition: null);
        }
    }

    /// <summary>
    /// Finds the best PowerShell executable: pwsh 7+ under Program Files,
    /// then pwsh anywhere on PATH, then Windows PowerShell 5.1 as a
    /// guaranteed-present fallback. Returns null on non-Windows.
    /// </summary>
    public static string? FindPowerShellExecutable()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var pwsh7 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwsh7)) return pwsh7;

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVar))
        {
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir, "pwsh.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { /* malformed PATH entry */ }
            }
        }

        var ps51 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(ps51) ? ps51 : null;
    }

    // ── Elevation detection ──────────────────────────────────────────────

    private static bool DetectElevation()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // platform-guarded above
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                return Geteuid() == 0;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Non-fatal: assume non-elevated if detection fails.
        }
        return false;
    }

    [DllImport("libc", EntryPoint = "geteuid", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32 | DllImportSearchPath.SafeDirectories)]
    private static extern uint Geteuid();
}

/// <summary>
/// Structured info about the PowerShell host Sovrant will use.
/// </summary>
/// <param name="Path">Full path to the executable.</param>
/// <param name="Version">Version string from <c>$PSVersionTable.PSVersion</c>, or null if probe failed.</param>
/// <param name="Edition">Either "Core" (pwsh 6+) or "Desktop" (Windows PowerShell 5.1), or null if probe failed.</param>
public sealed record PowerShellInfo(string Path, string? Version, string? Edition)
{
    /// <summary>True if this is PowerShell 7+ (cross-platform "Core" edition).</summary>
    public bool IsPwsh7Plus =>
        string.Equals(Edition, "Core", StringComparison.OrdinalIgnoreCase)
        || (Version is not null && Version.Length > 0 && Version[0] >= '6');

    /// <summary>True if this is legacy Windows PowerShell 5.1 (Desktop edition).</summary>
    public bool IsWindowsPowerShell51 =>
        string.Equals(Edition, "Desktop", StringComparison.OrdinalIgnoreCase)
        || (Version is not null && Version.StartsWith("5.", StringComparison.Ordinal));
}
