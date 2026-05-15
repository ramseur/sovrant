using Sovrant.Agents.Swarm;
using Sovrant.Commands.Commands;

namespace Sovrant.Commands.Tests.Commands;

public sealed class SwarmCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SwarmConfig _config;
    private readonly SwarmCommand _command;

    public SwarmCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-swarmcmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _config = new SwarmConfig();
        _command = new SwarmCommand(_config);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void Name_IsSwarm() => Assert.Equal("swarm", _command.Name);

    [Fact]
    public async Task NoArgs_HelpMentionsFile()
    {
        var result = await _command.ExecuteAsync("");
        Assert.Contains("--file", result.Output);
    }

    [Fact]
    public async Task FileFlag_LoadsContentFromDisk()
    {
        var path = Path.Combine(_tempDir, "prompt.md");
        await File.WriteAllTextAsync(path, "Build the thing carefully.");

        var result = await _command.ExecuteAsync($"--file {path}");

        Assert.Null(result.Output);
        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("Build the thing carefully.", result.InjectAsUserMessage);
        Assert.True(_config.Enabled);
    }

    [Fact]
    public async Task FileFlag_TrimsContent()
    {
        var path = Path.Combine(_tempDir, "prompt.md");
        await File.WriteAllTextAsync(path, "   \n  hello world  \n\n");

        var result = await _command.ExecuteAsync($"-f {path}");

        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("hello world", result.InjectAsUserMessage);
    }

    [Fact]
    public async Task FileFlag_AndInline_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "prompt.md");
        await File.WriteAllTextAsync(path, "content");

        var result = await _command.ExecuteAsync($"hello --file {path}");

        Assert.NotNull(result.Output);
        Assert.Contains("either", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.InjectAsUserMessage);
    }

    [Fact]
    public async Task FileFlag_MissingFile_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.md");

        var result = await _command.ExecuteAsync($"--file {path}");

        Assert.NotNull(result.Output);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.InjectAsUserMessage);
    }

    [Fact]
    public async Task FileFlag_EmptyFile_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "empty.md");
        await File.WriteAllTextAsync(path, "   \n  \t\n");

        var result = await _command.ExecuteAsync($"--file {path}");

        Assert.NotNull(result.Output);
        Assert.Contains("empty", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.InjectAsUserMessage);
    }

    [Fact]
    public async Task FileFlag_MissingPath_ReturnsError()
    {
        var result = await _command.ExecuteAsync("--file");

        Assert.NotNull(result.Output);
        Assert.Contains("requires a path", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InlinePrompt_InjectsAsUserMessage()
    {
        var result = await _command.ExecuteAsync("refactor the parser");

        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("refactor the parser", result.InjectAsUserMessage);
        Assert.True(_config.Enabled);
    }

    [Fact]
    public async Task InlinePrompt_DryRun_AddsSuffix()
    {
        var result = await _command.ExecuteAsync("refactor the parser --dry-run");

        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("dry_run", result.InjectAsUserMessage);
    }

    [Fact]
    public async Task Enable_SetsConfig()
    {
        var result = await _command.ExecuteAsync("enable");
        Assert.True(_config.Enabled);
        Assert.Contains("enabled", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disable_SetsConfig()
    {
        _config.Enabled = true;
        var result = await _command.ExecuteAsync("disable");
        Assert.False(_config.Enabled);
        Assert.Contains("disabled", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Yolo_WithFileFlag_LoadsAndRuns()
    {
        var path = Path.Combine(_tempDir, "prompt.md");
        await File.WriteAllTextAsync(path, "yolo task content");

        var result = await _command.ExecuteAsync($"yolo --file {path}");

        Assert.Equal("yolo", _config.Permissions);
        Assert.True(_config.Enabled);
        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("yolo task content", result.InjectAsUserMessage);
    }

    [Fact]
    public async Task AcceptEdits_WithFileFlag_LoadsAndRuns()
    {
        var path = Path.Combine(_tempDir, "prompt.md");
        await File.WriteAllTextAsync(path, "accept edits task");

        var result = await _command.ExecuteAsync($"accept-edits --file {path}");

        Assert.Equal("accept-edits", _config.Permissions);
        Assert.True(_config.Enabled);
        Assert.NotNull(result.InjectAsUserMessage);
        Assert.Contains("accept edits task", result.InjectAsUserMessage);
    }
}
