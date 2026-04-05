using Sovrant.Tools.Quality;

namespace Sovrant.Tools.Tests.Quality;

public sealed class DiffReviewerTests
{
    [Fact]
    public void Review_EmptyDiff_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DiffReviewer.Review(""));
        Assert.Equal(string.Empty, DiffReviewer.Review(null!));
    }

    [Fact]
    public void Review_CleanDiff_ReturnsEmpty()
    {
        var diff = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,3 +1,4 @@
             namespace Foo;
            +public class Bar { }
            """;
        Assert.Equal(string.Empty, DiffReviewer.Review(diff));
    }

    [Fact]
    public void Review_DetectsConsoleLog()
    {
        var diff = "+    console.log('debug stuff');";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("Debug code", result);
    }

    [Fact]
    public void Review_DetectsDebugger()
    {
        var diff = "+    debugger;";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("Debug code", result);
    }

    [Fact]
    public void Review_DetectsAwsKey()
    {
        var diff = "+const key = \"AKIAIOSFODNN7EXAMPLE\";";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("secret", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_DetectsPrivateKey()
    {
        var diff = "+-----BEGIN PRIVATE KEY-----";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("secret", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_DetectsEnvFile()
    {
        var diff = "diff --git a/.env b/.env\n+SECRET=foo";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("unintended", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_DetectsNodeModules()
    {
        var diff = "diff --git a/node_modules/foo/index.js b/node_modules/foo/index.js\n+module.exports = {};";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("unintended", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_DetectsTodo()
    {
        var diff = "+    // TODO: fix this later";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("TODO", result);
    }

    [Fact]
    public void Review_DetectsFixme()
    {
        var diff = "+    // FIXME: broken";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("FIXME", result);
    }

    [Fact]
    public void Review_DetectsHack()
    {
        var diff = "+    // HACK: workaround";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("HACK", result);
    }

    [Fact]
    public void Review_MultipleFindings()
    {
        var diff = "+console.log('test');\n+// TODO: remove\ndiff --git a/.env b/.env\n+X=1";
        var result = DiffReviewer.Review(diff);
        Assert.Contains("Debug code", result);
        Assert.Contains("TODO", result);
        Assert.Contains("unintended", result, StringComparison.OrdinalIgnoreCase);
    }
}
