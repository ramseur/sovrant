using System.Text.RegularExpressions;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Phase 144 guard: every variable documented in <c>.env.example</c> must actually be read by the
/// code. A documented variable that nothing reads is a setting users rely on that silently does
/// nothing (as SOVRANT_MODEL and LLM_API_KEY did before Phase 144).
/// </summary>
public sealed partial class EnvExampleGuardTests
{
    [GeneratedRegex(@"^#?\s*([A-Z][A-Z0-9_]{2,})=", RegexOptions.Multiline)]
    private static partial Regex VariableLine();

    [Fact]
    public void Every_Documented_Env_Variable_Is_Read_By_The_Code()
    {
        var root = RepoRoot();
        var documented = VariableLine().Matches(File.ReadAllText(Path.Combine(root, ".env.example")))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(documented);

        var source = string.Join('\n', Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        var unread = documented.Where(v => !source.Contains($"\"{v}\"", StringComparison.Ordinal)).ToList();
        Assert.True(unread.Count == 0, "Documented in .env.example but never read in src/: " + string.Join(", ", unread));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Sovrant.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Sovrant.slnx not found above the test output directory");
    }
}
