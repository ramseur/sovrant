using Sovrant.Api.Types;
using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class PromptSanitizerTests
{
    private static PromptSanitizer CreateSanitizer(CorporateDataConfig? corpConfig = null)
    {
        IPatternDetector[] detectors =
        [
            new PiiDetector(),
            new CorporateDataDetector(corpConfig),
        ];
        return new PromptSanitizer(detectors);
    }

    [Fact]
    public void RoundTrip_SanitizeAndRestore()
    {
        var sanitizer = CreateSanitizer(new CorporateDataConfig
        {
            CorporateDomains = ["acme-corp.internal"],
        });

        var request = new MessagesRequest("gpt-4o-mini", 1024,
        [
            InputMessage.UserText("Fix the auth bug in api.acme-corp.internal for john.doe@acme.com"),
        ]);

        var result = sanitizer.Sanitize(request);

        // Verify placeholders are present in sanitized text.
        var sanitizedText = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        Assert.Contains("[EMAIL_1]", sanitizedText);
        Assert.Contains("[HOSTNAME_1]", sanitizedText);
        Assert.DoesNotContain("john.doe@acme.com", sanitizedText);
        Assert.DoesNotContain("api.acme-corp.internal", sanitizedText);

        // Verify round-trip restoration.
        var restored = sanitizer.Restore(sanitizedText, result.Map);
        Assert.Contains("john.doe@acme.com", restored);
        Assert.Contains("api.acme-corp.internal", restored);
    }

    [Fact]
    public void SystemPrompt_IsSanitized()
    {
        var sanitizer = CreateSanitizer();
        var request = new MessagesRequest("gpt-4o-mini", 1024,
            [InputMessage.UserText("hello")])
        {
            System = "Admin email: admin@company.com",
        };

        var result = sanitizer.Sanitize(request);
        Assert.DoesNotContain("admin@company.com", result.Request.System);
        Assert.Contains("[EMAIL_1]", result.Request.System);
    }

    [Fact]
    public void NoSensitiveData_PassesThrough()
    {
        var sanitizer = CreateSanitizer();
        var request = new MessagesRequest("gpt-4o-mini", 1024,
            [InputMessage.UserText("Fix the login button alignment")]);

        var result = sanitizer.Sanitize(request);
        var text = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        Assert.Equal("Fix the login button alignment", text);
        Assert.True(result.Map.IsEmpty);
    }

    [Fact]
    public void MultiplePiiTypes_AllRedacted()
    {
        var sanitizer = CreateSanitizer();
        var request = new MessagesRequest("gpt-4o-mini", 1024,
        [
            InputMessage.UserText("User a@b.com at 10.0.0.1 with SSN 123-45-6789"),
        ]);

        var result = sanitizer.Sanitize(request);
        var text = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        Assert.DoesNotContain("a@b.com", text);
        Assert.DoesNotContain("10.0.0.1", text);
        Assert.DoesNotContain("123-45-6789", text);
        Assert.True(result.Map.Count >= 3);
    }

    [Fact]
    public void SameValueAppearingTwice_SamePlaceholder()
    {
        var sanitizer = CreateSanitizer();
        var request = new MessagesRequest("gpt-4o-mini", 1024,
        [
            InputMessage.UserText("Email a@b.com and again a@b.com"),
        ]);

        var result = sanitizer.Sanitize(request);
        var text = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        // Both occurrences should use the same placeholder.
        Assert.Equal(1, result.Map.Count);
    }

    [Fact]
    public void ConnectionString_IsRedacted()
    {
        var sanitizer = CreateSanitizer();
        var request = new MessagesRequest("gpt-4o-mini", 1024,
        [
            InputMessage.UserText("Connect to postgres://admin:pass@db.local:5432/mydb"),
        ]);

        var result = sanitizer.Sanitize(request);
        var text = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        Assert.DoesNotContain("postgres://", text);
        Assert.Contains("[CONNECTION_STRING_1]", text);
    }

    [Fact]
    public void ToolsAndMetadata_Preserved()
    {
        var sanitizer = CreateSanitizer();
        var tools = new List<ToolDefinition>
        {
            new("read_file", new System.Text.Json.JsonElement()) { Description = "Reads a file" },
        };
        var request = new MessagesRequest("gpt-4o-mini", 1024,
            [InputMessage.UserText("hello")])
        {
            Tools = tools,
            Stream = true,
        };

        var result = sanitizer.Sanitize(request);
        Assert.Equal("gpt-4o-mini", result.Request.Model);
        Assert.Equal(1024, result.Request.MaxTokens);
        Assert.True(result.Request.Stream);
        Assert.NotNull(result.Request.Tools);
        Assert.Single(result.Request.Tools);
    }

    [Fact]
    public void CustomPatterns_Detected()
    {
        IPatternDetector[] detectors =
        [
            new PiiDetector(),
            new CustomPatternRegistry([new CustomPattern("codenames", @"\bProject\s+Titan\b")]),
        ];
        var sanitizer = new PromptSanitizer(detectors);

        var request = new MessagesRequest("gpt-4o-mini", 1024,
            [InputMessage.UserText("Working on Project Titan deployment")]);

        var result = sanitizer.Sanitize(request);
        var text = ((InputContentBlock.TextBlock)result.Request.Messages[0].Content[0]).Text;
        Assert.DoesNotContain("Project Titan", text);
        Assert.Contains("[CODENAMES_1]", text);
    }
}
