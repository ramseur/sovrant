namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Phase 145 stopgap guard: Sovrant.Web must not save the Web sign-in token or restore it at
/// startup. It used to, process-wide, so after a restart every visitor was signed in as the last
/// user with no password. Until per-browser sign-in (Phase 145) lands, the token lives in memory only.
/// </summary>
public sealed class WebSignInGuardTests
{
    private const string WebTokenKey = "sovrant.web.auth_token";

    [Fact]
    public void Web_Never_Saves_Or_Restores_A_Sign_In_Token()
    {
        var webDir = Path.Combine(RepoRoot(), "src", "Sovrant.Web");
        var files = Directory.EnumerateFiles(webDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (!text.Contains(WebTokenKey, StringComparison.Ordinal)) continue;
            // Only clean-up (DeleteAsync) may refer to the key; StoreAsync / RetrieveAsync bring the bug back.
            Assert.False(text.Contains("StoreAsync(StoredWebTokenKey", StringComparison.Ordinal)
                      || text.Contains("StoreAsync(StoredTokenKey", StringComparison.Ordinal)
                      || text.Contains("RetrieveAsync(StoredWebTokenKey", StringComparison.Ordinal)
                      || text.Contains("RetrieveAsync(StoredTokenKey", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} saves or restores the Web sign-in token");
        }
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Sovrant.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Sovrant.slnx not found above the test output directory");
    }
}
