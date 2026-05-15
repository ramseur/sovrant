using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class SecretDetectorTests
{
    private readonly SecretDetector _detector = new();

    [Fact]
    public void Scan_Null_ReturnsNull() => Assert.Null(_detector.Scan(null!));

    [Fact]
    public void Scan_Empty_ReturnsNull() => Assert.Null(_detector.Scan(""));

    [Fact]
    public void Scan_CleanText_ReturnsNull() =>
        Assert.Null(_detector.Scan("This is just normal code with no secrets."));

    [Fact]
    public void Scan_AwsKey_Detects() =>
        Assert.Contains("AWS", _detector.Scan("AKIAIOSFODNN7EXAMPLE")!);

    [Fact]
    public void Scan_SkKey_Detects() =>
        Assert.Contains("API key", _detector.Scan("sk-abcdefghijklmnopqrstuvwxyz1234567890")!);

    [Fact]
    public void Scan_ApiKeyAssignment_Detects() =>
        Assert.Contains("API key", _detector.Scan("api_key = \"abcdefghijklmnopqrstuvwxyz\"")!);

    [Fact]
    public void Scan_PrivateKey_Detects() =>
        Assert.Contains("Private key", _detector.Scan("-----BEGIN PRIVATE KEY-----\nMIIE...")!);

    [Fact]
    public void Scan_RsaPrivateKey_Detects() =>
        Assert.Contains("Private key", _detector.Scan("-----BEGIN RSA PRIVATE KEY-----\nMIIE...")!);

    [Fact]
    public void Scan_Jwt_Detects() =>
        Assert.Contains("JWT", _detector.Scan("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123def456ghi789")!);

    [Fact]
    public void Scan_Password_Detects() =>
        Assert.Contains("password", _detector.Scan("password = \"supersecret123\"")!, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Scan_BearerToken_Detects() =>
        Assert.Contains("Token", _detector.Scan("bearer abcdefghijklmnopqrstuvwxyz1234567890")!);

    [Fact]
    public void Scan_TokenAssignment_Detects() =>
        Assert.Contains("Token", _detector.Scan("token = \"abcdefghijklmnopqrstuvwxyz1234567890\"")!);

    [Fact]
    public void Scan_CustomPattern_Detects()
    {
        var detector = new SecretDetector(["CUSTOM_[0-9]{10}"]);
        Assert.Contains("Custom", detector.Scan("Found CUSTOM_1234567890 in output")!);
    }

    [Fact]
    public void Scan_InvalidCustomPattern_Ignored()
    {
        var detector = new SecretDetector(["[invalid(("]);
        // Should not throw, and should still detect built-in patterns
        Assert.Null(detector.Scan("clean text"));
    }
}
