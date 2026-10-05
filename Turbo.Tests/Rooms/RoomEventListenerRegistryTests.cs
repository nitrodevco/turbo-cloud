using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Rooms;
using Turbo.Rooms.Grains.Systems;
using Turbo.Runtime.AssemblyProcessing;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A plugin's <see cref="IRoomEventListener"/>s are found by the assembly scan, built with the
/// plugin's services, and removed again when the plugin unloads.
/// </summary>
public sealed class RoomEventListenerRegistryTests
{
    private readonly RoomEventListenerRegistry _registry = new();

    private static IServiceProvider Services() =>
        new ServiceCollection()
            .AddSingleton(new ScannedListenerDependency("built"))
            .BuildServiceProvider();

    private IAssemblyFeatureProcessor Processor() =>
        (IAssemblyFeatureProcessor)
            Activator.CreateInstance(
                typeof(RoomEventListenerRegistry).Assembly.GetType(
                    "Turbo.Rooms.RoomEventListenerFeatureProcessor"
                )!,
                _registry
            )!;

    [Fact]
    public async Task TheScanRegistersAPublicListenerAndUnloadingRemovesIt()
    {
        var registration = await Processor()
            .ProcessAsync(typeof(ScannedListener).Assembly, Services());

        _registry
            .Listeners.OfType<ScannedListener>()
            .Should()
            .ContainSingle()
            .Which.Dependency.Name.Should()
            .Be("built");

        registration.Dispose();

        _registry.Listeners.OfType<ScannedListener>().Should().BeEmpty();
    }

    [Fact]
    public async Task ADisposableListenerIsDisposedWhenTheRegistrationIs()
    {
        var registration = await Processor()
            .ProcessAsync(typeof(ScannedListener).Assembly, Services());
        var listener = _registry.Listeners.OfType<ScannedListener>().Single();

        registration.Dispose();

        listener.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task TheScanLeavesTheRoomsOwnSystemsAlone()
    {
        using var registration = await Processor()
            .ProcessAsync(typeof(RoomEventListenerRegistry).Assembly, Services());

        _registry.Listeners.Should().BeEmpty();
        typeof(RoomWaterAreaSystem).Should().Implement<IRoomEventListener>();
    }

    [Fact]
    public void DisposingARegistrationTwiceKeepsAnotherRegistrationsListener()
    {
        var kept = new ScannedListener(new ScannedListenerDependency("kept"));
        var other = new ScannedListener(new ScannedListenerDependency("other"));
        _registry.Register([kept]);
        var registration = _registry.Register([other]);

        registration.Dispose();
        registration.Dispose();

        _registry.Listeners.Should().Equal(kept);
    }

    [Fact]
    public void AListSeenByARoomMidPublishIsNeverChanged()
    {
        var first = new ScannedListener(new ScannedListenerDependency("a"));
        var registration = _registry.Register([first]);
        var seen = _registry.Listeners;

        registration.Dispose();

        seen.Should().Equal(first);
        _registry.Listeners.Should().BeEmpty();
    }
}

public sealed record ScannedListenerDependency(string Name);

/// <summary>Public and top level, as the scan requires of a plugin's listener.</summary>
public sealed class ScannedListener(ScannedListenerDependency dependency)
    : IRoomEventListener,
        IDisposable
{
    public ScannedListenerDependency Dependency { get; } = dependency;
    public bool Disposed { get; private set; }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct) => Task.CompletedTask;

    public void Dispose() => Disposed = true;
}
