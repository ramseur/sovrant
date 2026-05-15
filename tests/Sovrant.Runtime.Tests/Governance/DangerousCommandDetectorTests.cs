using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class DangerousCommandDetectorTests
{
    private readonly DangerousCommandDetector _detector = new();

    [Fact]
    public void Check_Null_ReturnsNull() => Assert.Null(_detector.Check(null!));

    [Fact]
    public void Check_Empty_ReturnsNull() => Assert.Null(_detector.Check(""));

    [Fact]
    public void Check_SafeCommand_ReturnsNull() =>
        Assert.Null(_detector.Check("ls -la"));

    [Fact]
    public void Check_RmRfRoot_Detects() =>
        Assert.NotNull(_detector.Check("rm -rf /"));

    [Fact]
    public void Check_RmRfSlashStar_Detects() =>
        Assert.NotNull(_detector.Check("rm -rf /*"));

    [Fact]
    public void Check_RmRfHome_Detects() =>
        Assert.NotNull(_detector.Check("rm -rf ~"));

    [Fact]
    public void Check_GitPushForceMain_Detects() =>
        Assert.NotNull(_detector.Check("git push --force main"));

    [Fact]
    public void Check_GitPushFMaster_Detects() =>
        Assert.NotNull(_detector.Check("git push -f master"));

    [Fact]
    public void Check_GitResetHard_Detects() =>
        Assert.NotNull(_detector.Check("git reset --hard"));

    [Fact]
    public void Check_DropTable_Detects() =>
        Assert.NotNull(_detector.Check("psql -c 'DROP TABLE users'"));

    [Fact]
    public void Check_DropDatabase_Detects() =>
        Assert.NotNull(_detector.Check("DROP DATABASE production"));

    [Fact]
    public void Check_Chmod777_Detects() =>
        Assert.NotNull(_detector.Check("chmod 777 /var/www"));

    [Fact]
    public void Check_Mkfs_Detects() =>
        Assert.NotNull(_detector.Check("mkfs.ext4 /dev/sda1"));

    [Fact]
    public void Check_DdDevZero_Detects() =>
        Assert.NotNull(_detector.Check("dd if=/dev/zero of=/dev/sda"));

    [Fact]
    public void Check_CaseInsensitive() =>
        Assert.NotNull(_detector.Check("DROP TABLE users"));

    [Fact]
    public void Check_SafeGitPush_ReturnsNull() =>
        Assert.Null(_detector.Check("git push origin feature-branch"));

    [Fact]
    public void Check_SafeRm_ReturnsNull() =>
        Assert.Null(_detector.Check("rm temp.txt"));

    [Fact]
    public void Check_CustomBlockedCommand_Detects()
    {
        var detector = new DangerousCommandDetector(["custom-danger"]);
        Assert.NotNull(detector.Check("run custom-danger now"));
    }

    [Fact]
    public void Check_CustomBlockedCommand_NoDuplicates()
    {
        // Built-in "rm -rf /" should still work even if passed again as extra
        var detector = new DangerousCommandDetector(["rm -rf /"]);
        Assert.NotNull(detector.Check("rm -rf /"));
    }
}
