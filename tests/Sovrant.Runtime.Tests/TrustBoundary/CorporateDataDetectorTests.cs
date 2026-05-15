using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class CorporateDataDetectorTests
{
    [Fact]
    public void DetectsPostgresConnectionString()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Connect to postgres://admin:s3cret@db.local:5432/users");
        Assert.Contains(detections, d => d.Category == "CONNECTION_STRING");
    }

    [Fact]
    public void DetectsMongoConnectionString()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Use mongodb+srv://user:pass@cluster.mongodb.net/db");
        Assert.Contains(detections, d => d.Category == "CONNECTION_STRING");
    }

    [Fact]
    public void DetectsServerEqualsConnectionString()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Server=sql.acme.com;Database=prod;User=sa;Password=p@ss");
        Assert.Contains(detections, d => d.Category == "CONNECTION_STRING");
    }

    [Fact]
    public void DetectsApiKeys()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Key is sk-proj-abcdefghij1234567890");
        Assert.Contains(detections, d => d.Category == "API_KEY");
    }

    [Fact]
    public void DetectsGitHubPat()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Token: ghp_abcdefghij1234567890");
        Assert.Contains(detections, d => d.Category == "API_KEY");
    }

    [Fact]
    public void DetectsBearerToken()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature");
        Assert.Contains(detections, d => d.Category == "API_KEY");
    }

    [Fact]
    public void DetectsAwsArn()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("Resource: arn:aws:s3:us-east-1:123456789012:my-bucket");
        Assert.Contains(detections, d => d.Category == "CLOUD_RESOURCE");
    }

    [Fact]
    public void DetectsAzureBlobUrl()
    {
        var detector = new CorporateDataDetector();
        var detections = detector.Detect("File at https://myaccount.blob.core.windows.net/container/blob");
        Assert.Contains(detections, d => d.Category == "CLOUD_RESOURCE");
    }

    [Fact]
    public void DetectsInternalHostnames()
    {
        var config = new CorporateDataConfig { CorporateDomains = ["acme-corp.internal"] };
        var detector = new CorporateDataDetector(config);
        var detections = detector.Detect("Bug in api.acme-corp.internal");
        Assert.Contains(detections, d => d.Category == "HOSTNAME" && d.OriginalValue == "api.acme-corp.internal");
    }

    [Fact]
    public void AllowList_SkipsAllowedDomains()
    {
        var config = new CorporateDataConfig
        {
            CorporateDomains = ["com"],
            AllowList = ["github.com"],
        };
        var detector = new CorporateDataDetector(config);
        var detections = detector.Detect("See api.github.com for the spec");
        Assert.DoesNotContain(detections, d => d.Category == "HOSTNAME");
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        var detector = new CorporateDataDetector();
        Assert.Empty(detector.Detect(""));
    }
}
