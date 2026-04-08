namespace Sovrant.Agents.Swarm;

/// <summary>
/// Helpers for loading a swarm master prompt from a file on disk. Used by both
/// the <c>sovrant swarm --file</c> CLI subcommand and the <c>/swarm --file</c>
/// REPL slash command so the path-resolution and trimming rules stay in one
/// place.
/// </summary>
public static class SwarmPromptFile
{
    /// <summary>
    /// Resolves a user-supplied path. Expands a leading <c>~</c> to the user's
    /// profile directory and leaves everything else for the OS to interpret
    /// (so relative paths resolve against the current working directory and
    /// absolute paths pass through unchanged).
    /// </summary>
    public static string ResolvePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.Length >= 1 && path[0] == '~')
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            // Handle "~", "~/foo", and "~\foo".
            if (path.Length == 1)
                return home;
            if (path[1] == '/' || path[1] == '\\')
                return Path.Combine(home, path[2..]);
        }

        return path;
    }

    /// <summary>
    /// Reads a master prompt from disk. Throws <see cref="FileNotFoundException"/>
    /// if the resolved path does not exist and <see cref="InvalidOperationException"/>
    /// if the file is empty or whitespace-only — both conditions that would
    /// otherwise cause a confusing failure deep inside the swarm decomposer.
    /// </summary>
    public static async Task<string> LoadAsync(string path, CancellationToken ct = default)
    {
        var resolved = ResolvePath(path);
        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Swarm prompt file not found: {resolved}", resolved);

        var content = (await File.ReadAllTextAsync(resolved, ct).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"Swarm prompt file is empty: {resolved}");

        return content;
    }
}
