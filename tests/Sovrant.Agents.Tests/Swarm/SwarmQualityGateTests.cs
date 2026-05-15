using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class SwarmQualityGateTests
{
    [Fact]
    public void ParseVerdict_ValidJson_Parses()
    {
        var output = """{"Score": 8, "Verdict": "pass", "Feedback": "Looks good."}""";
        var verdict = SwarmQualityGate.ParseVerdict(output);
        Assert.NotNull(verdict);
        Assert.Equal(8, verdict.Score);
        Assert.Equal("pass", verdict.Verdict);
        Assert.Equal("Looks good.", verdict.Feedback);
    }

    [Fact]
    public void ParseVerdict_FencedJson_Parses()
    {
        var output = "Here is my review:\n```json\n{\"Score\": 3, \"Verdict\": \"fail\", \"Feedback\": \"Missing tests.\"}\n```";
        var verdict = SwarmQualityGate.ParseVerdict(output);
        Assert.NotNull(verdict);
        Assert.Equal(3, verdict.Score);
        Assert.Equal("fail", verdict.Verdict);
    }

    [Fact]
    public void ParseVerdict_StructuredText_Parses()
    {
        var output = "SCORE: 6\nVERDICT: needs_revision\nFEEDBACK: Could be better.";
        var verdict = SwarmQualityGate.ParseVerdict(output);
        Assert.NotNull(verdict);
        Assert.Equal(6, verdict.Score);
        Assert.Equal("needs_revision", verdict.Verdict);
        Assert.Equal("Could be better.", verdict.Feedback);
    }

    [Fact]
    public void ParseVerdict_StructuredText_CaseInsensitive()
    {
        var output = "score: 9\nverdict: PASS\nfeedback: All good.";
        var verdict = SwarmQualityGate.ParseVerdict(output);
        Assert.NotNull(verdict);
        Assert.Equal(9, verdict.Score);
        Assert.Equal("PASS", verdict.Verdict);
    }

    [Fact]
    public void ParseVerdict_MalformedOutput_ReturnsNull()
    {
        var output = "I think the code looks reasonable overall.";
        Assert.Null(SwarmQualityGate.ParseVerdict(output));
    }

    [Fact]
    public void ParseVerdict_PartialStructuredText_NoVerdict_ReturnsNull()
    {
        var output = "SCORE: 7\nThis is pretty good overall.";
        Assert.Null(SwarmQualityGate.ParseVerdict(output));
    }

    [Fact]
    public void ParseVerdict_EmptyOutput_ReturnsNull()
    {
        Assert.Null(SwarmQualityGate.ParseVerdict(""));
    }

    [Fact]
    public void ParseVerdict_JsonWithExtraContent_Parses()
    {
        var output = "After careful review, I conclude:\n{\"Score\": 7, \"Verdict\": \"pass\", \"Feedback\": \"Minor issues.\"}\nEnd of review.";
        var verdict = SwarmQualityGate.ParseVerdict(output);
        Assert.NotNull(verdict);
        Assert.Equal(7, verdict.Score);
    }
}
