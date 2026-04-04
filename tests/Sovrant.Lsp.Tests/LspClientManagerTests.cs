using Microsoft.Extensions.Logging.Abstractions;

namespace Sovrant.Lsp.Tests;

/// <summary>Tests for <see cref="LspClientManager"/>.</summary>
public sealed class LspClientManagerTests
{
    private static LspClientManager CreateManager(params LspServerConfig[] configs) =>
        new(configs, NullLoggerFactory.Instance);

    [Fact]
    public void Languages_Empty_WhenNoConfigs()
    {
        var mgr = CreateManager();
        Assert.Empty(mgr.Languages);
    }

    [Fact]
    public void Languages_ReturnsConfiguredLanguages()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" },
            new LspServerConfig { Language = "python", Command = "pyright-langserver" });

        Assert.Equal(2, mgr.Languages.Count);
        Assert.Contains("csharp", mgr.Languages);
        Assert.Contains("python", mgr.Languages);
    }

    [Fact]
    public void GetClient_ReturnsClient_ForConfiguredLanguage()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        var client = mgr.GetClient("csharp");
        Assert.NotNull(client);
        Assert.Equal("csharp", client.Language);
    }

    [Fact]
    public void GetClient_ReturnsNull_ForUnconfiguredLanguage()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        Assert.Null(mgr.GetClient("python"));
    }

    [Fact]
    public void GetClientForFile_MatchesCsharpExtension()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        var client = mgr.GetClientForFile("src/Program.cs");
        Assert.NotNull(client);
        Assert.Equal("csharp", client.Language);
    }

    [Fact]
    public void GetClientForFile_MatchesPythonExtension()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "python", Command = "pyright-langserver" });

        var client = mgr.GetClientForFile("scripts/main.py");
        Assert.NotNull(client);
        Assert.Equal("python", client.Language);
    }

    [Fact]
    public void GetClientForFile_MatchesTypescriptExtensions()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "typescript", Command = "typescript-language-server" });

        Assert.NotNull(mgr.GetClientForFile("app.ts"));
        Assert.NotNull(mgr.GetClientForFile("component.tsx"));
    }

    [Fact]
    public void GetClientForFile_ReturnsNull_ForUnknownExtension()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        Assert.Null(mgr.GetClientForFile("data.csv"));
    }

    [Fact]
    public void GetClientForFile_ReturnsNull_WhenLanguageNotConfigured()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        // .py maps to "python" but python is not configured
        Assert.Null(mgr.GetClientForFile("main.py"));
    }

    [Fact]
    public void Constructor_SkipsBlankConfigs()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "", Command = "omnisharp" },
            new LspServerConfig { Language = "python", Command = "" },
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        Assert.Single(mgr.Languages);
        Assert.Equal("csharp", mgr.Languages[0]);
    }

    [Fact]
    public void IsRunning_FalseBeforeStart()
    {
        var mgr = CreateManager(
            new LspServerConfig { Language = "csharp", Command = "omnisharp" });

        var client = mgr.GetClient("csharp");
        Assert.NotNull(client);
        Assert.False(client.IsRunning);
    }
}
