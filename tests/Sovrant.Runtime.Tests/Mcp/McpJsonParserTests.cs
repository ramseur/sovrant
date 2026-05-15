using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

public sealed class McpJsonParserTests
{
    [Fact]
    public void Parses_PixelLab_HttpWrapped_Format()
    {
        const string json = """
        {
          "mcpServers": {
            "pixellab": {
              "url": "https://api.pixellab.ai/mcp",
              "transport": "http",
              "headers": {
                "Authorization": "Bearer abc-123"
              }
            }
          }
        }
        """;

        var entries = McpJsonParser.Parse(json);

        Assert.Single(entries);
        var pixellab = entries["pixellab"];
        Assert.Equal(new Uri("https://api.pixellab.ai/mcp"), pixellab.Url);
        Assert.Equal("Bearer abc-123", pixellab.Headers["Authorization"]);
        Assert.Empty(pixellab.Command);
    }

    [Fact]
    public void Parses_StdioWrapped_Format()
    {
        const string json = """
        {
          "mcpServers": {
            "filesystem": {
              "command": "npx",
              "args": ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"]
            }
          }
        }
        """;

        var entries = McpJsonParser.Parse(json);

        Assert.Single(entries);
        var fs = entries["filesystem"];
        Assert.Equal("npx", fs.Command);
        Assert.Equal(["-y", "@modelcontextprotocol/server-filesystem", "/tmp"], fs.Args);
        Assert.Null(fs.Url);
    }

    [Fact]
    public void Parses_BareServerObject_UsesDefaultName()
    {
        const string json = """{ "url": "https://x.example/mcp" }""";

        var entries = McpJsonParser.Parse(json, defaultName: "x");

        Assert.Single(entries);
        Assert.True(entries.ContainsKey("x"));
    }

    [Fact]
    public void Parses_Multiple_Entries_Without_Wrapper()
    {
        const string json = """
        {
          "alpha": { "command": "alpha-bin" },
          "beta":  { "url": "https://beta/mcp" }
        }
        """;

        var entries = McpJsonParser.Parse(json);

        Assert.Equal(2, entries.Count);
        Assert.Equal("alpha-bin", entries["alpha"].Command);
        Assert.Equal(new Uri("https://beta/mcp"), entries["beta"].Url);
    }

    [Fact]
    public void Throws_OnEmpty()
    {
        Assert.Throws<ArgumentException>(() => McpJsonParser.Parse(""));
    }

    [Fact]
    public void Throws_OnEntryWithoutUrlOrCommand()
    {
        const string json = """{ "broken": { "args": ["x"] } }""";
        Assert.Throws<System.Text.Json.JsonException>(() => McpJsonParser.Parse(json));
    }
}
