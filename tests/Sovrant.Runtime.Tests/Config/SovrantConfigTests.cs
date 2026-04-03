using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>Tests for <see cref="SovrantConfig"/> defaults.</summary>
public sealed class SovrantConfigTests
{
    [Fact]
    public void SovrantConfig_Defaults_AreCorrect()
    {
        var config = new SovrantConfig();

        Assert.Equal("claude-sonnet-4-6", config.Model);
        Assert.Equal(8192, config.MaxTokens);
        Assert.Equal(PermissionMode.Default, config.PermissionMode);
        Assert.Equal(RouterMode.Smart, config.RouterMode);
        Assert.Equal(RouterStrategy.Balanced, config.RouterStrategy);
        Assert.Null(config.BaseUrl);
        Assert.Null(config.ApiKey);
        Assert.Empty(config.McpServers);
    }
}
