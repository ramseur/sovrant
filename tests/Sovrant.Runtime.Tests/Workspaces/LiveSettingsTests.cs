using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Workspaces;

public sealed class LiveSettingsTests
{
    private sealed class Box(int value) { public int Value { get; } = value; }

    [Fact]
    public void Current_ReflectsInitialResolve()
    {
        var live = new LiveSettings<Box>(() => new Box(7));
        Assert.Equal(7, live.Current.Value);
    }

    [Fact]
    public void Reload_SwapsCurrentAndNotifiesSubscriber()
    {
        var counter = 0;
        var live = new LiveSettings<Box>(() => new Box(++counter));
        Box? observed = null;
        live.OnChanged(fresh => observed = fresh);

        Assert.Equal(1, live.Current.Value);
        live.Reload();
        Assert.Equal(2, live.Current.Value);
        Assert.NotNull(observed);
        Assert.Equal(2, observed!.Value);
    }

    [Fact]
    public void Reload_SwallowsHandlerExceptionAndContinuesFanOut()
    {
        var live = new LiveSettings<Box>(() => new Box(0));
        var sawSecond = false;
        live.OnChanged(_ => throw new InvalidOperationException("bad"));
        live.OnChanged(_ => sawSecond = true);

        live.Reload(); // must not throw

        Assert.True(sawSecond);
    }

    [Fact]
    public void Subscription_Dispose_StopsNotifications()
    {
        var live = new LiveSettings<Box>(() => new Box(0));
        var count = 0;
        var sub = live.OnChanged(_ => count++);

        live.Reload();
        Assert.Equal(1, count);

        sub.Dispose();
        live.Reload();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Registry_FansOutReloadToAllWrappers()
    {
        var aResolves = 0;
        var bResolves = 0;
        var liveA = new LiveSettings<Box>(() => new Box(++aResolves));
        var liveB = new LiveSettings<Box>(() => new Box(++bResolves));

        var registry = new LiveSettingsRegistry();
        registry.Register(liveA);
        registry.Register(liveB);

        Assert.Equal(1, liveA.Current.Value);
        Assert.Equal(1, liveB.Current.Value);

        registry.ReloadAll();

        Assert.Equal(2, liveA.Current.Value);
        Assert.Equal(2, liveB.Current.Value);
    }
}
