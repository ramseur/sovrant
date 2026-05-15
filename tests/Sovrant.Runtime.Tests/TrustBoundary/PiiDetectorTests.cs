using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class PiiDetectorTests
{
    private readonly PiiDetector _detector = new();

    [Fact]
    public void DetectsEmailAddresses()
    {
        var detections = _detector.Detect("Contact john.doe@acme.com for details");
        Assert.Contains(detections, d => d.Category == "EMAIL" && d.OriginalValue == "john.doe@acme.com");
    }

    [Fact]
    public void DetectsMultipleEmails()
    {
        var detections = _detector.Detect("From alice@a.com to bob@b.com");
        Assert.Equal(2, detections.Count(d => d.Category == "EMAIL"));
    }

    [Fact]
    public void DetectsPhoneNumbers()
    {
        var detections = _detector.Detect("Call +1-555-123-4567 or (555) 987-6543");
        Assert.True(detections.Count(d => d.Category == "PHONE") >= 2);
    }

    [Fact]
    public void DetectsSsn()
    {
        var detections = _detector.Detect("SSN is 123-45-6789");
        Assert.Contains(detections, d => d.Category == "SSN" && d.OriginalValue == "123-45-6789");
    }

    [Fact]
    public void DetectsCreditCardNumbers()
    {
        var detections = _detector.Detect("Card: 4111-1111-1111-1111");
        Assert.Contains(detections, d => d.Category == "CARD");
    }

    [Fact]
    public void DetectsInternalIpAddresses()
    {
        var detections = _detector.Detect("Server at 10.0.1.50 and 192.168.1.1");
        Assert.Equal(2, detections.Count(d => d.Category == "IP"));
    }

    [Fact]
    public void DoesNotDetectPublicIps()
    {
        var detections = _detector.Detect("Server at 8.8.8.8");
        Assert.DoesNotContain(detections, d => d.Category == "IP");
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Empty(_detector.Detect(""));
        Assert.Empty(_detector.Detect(null!));
    }

    [Fact]
    public void NoSensitiveData_ReturnsEmpty()
    {
        Assert.Empty(_detector.Detect("Fix the authentication bug in the login flow"));
    }

    [Fact]
    public void DetectionsAreSortedByPosition()
    {
        var detections = _detector.Detect("Email: a@b.com, IP: 10.0.0.1, SSN: 123-45-6789");
        for (var i = 1; i < detections.Count; i++)
        {
            Assert.True(detections[i].Start >= detections[i - 1].Start);
        }
    }
}
