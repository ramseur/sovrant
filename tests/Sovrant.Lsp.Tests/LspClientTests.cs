namespace Sovrant.Lsp.Tests;

/// <summary>Tests for <see cref="LspClient"/> utility methods and <see cref="LspClientManager"/>.</summary>
public sealed class LspClientTests
{
    [Theory]
    [InlineData(@"C:\Users\dev\project\src\main.cs", "file:///C:/Users/dev/project/src/main.cs")]
    [InlineData(@"C:\src\file.py", "file:///C:/src/file.py")]
    public void PathToUri_Converts_Windows_Path(string path, string expected)
    {
        var result = LspClient.PathToUri(path);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("file:///C:/Users/dev/project/src/main.cs", @"C:\Users\dev\project\src\main.cs")]
    public void UriToPath_Converts_FileUri(string uri, string expected)
    {
        var result = LspClient.UriToPath(uri);
        Assert.Equal(expected.Replace('\\', Path.DirectorySeparatorChar), result);
    }

    [Fact]
    public void UriToPath_Passthrough_NonFileUri()
    {
        var result = LspClient.UriToPath("https://example.com");
        Assert.Equal("https://example.com", result);
    }
}
