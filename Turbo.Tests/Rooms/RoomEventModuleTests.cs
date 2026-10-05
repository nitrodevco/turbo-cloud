using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room hands every event to its own systems and then to the listeners a plugin registered.
/// The plugin's code is not the room's, so a listener that throws must not break the room or
/// keep the listeners after it from hearing the event.
/// </summary>
public sealed class RoomEventModuleTests
{
    private readonly LiveRoomHarness _harness = new();
    private readonly CapturingLogger<IRoomGrain> _log = new();

    public RoomEventModuleTests() => RoomHarness.SetField(_harness.Room, "_logger", _log);

    private RoomEventModule Events => _harness.Module<RoomEventModule>();

    private static AvatarIdleChangedEvent Event() =>
        new()
        {
            RoomId = 1,
            ObjectId = 7,
            IsIdle = true,
        };

    [Fact]
    public async Task AListenerThatThrowsIsLoggedAndTheOthersStillHearTheEvent()
    {
        var after = new RecordingListener();
        using var registration = _harness.EventListeners.Register([new ThrowingListener(), after]);

        var publish = () => Events.PublishAsync(Event(), CancellationToken.None);

        await publish.Should().NotThrowAsync();
        after.Heard.Should().ContainSingle();
        _log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle(e =>
                e.Exception is InvalidOperationException
                && e.Message.Contains("ThrowingListener")
                && e.Message.Contains("AvatarIdleChangedEvent")
            );
    }

    [Fact]
    public async Task AListenerThatFailsAfterAnAwaitIsLoggedToo()
    {
        var after = new RecordingListener();
        using var registration = _harness.EventListeners.Register([
            new ThrowingListener(afterAwait: true),
            after,
        ]);

        await Events.PublishAsync(Event(), CancellationToken.None);

        after.Heard.Should().ContainSingle();
        _log.AtLeast(LogLevel.Error).Should().ContainSingle();
    }

    [Fact]
    public async Task ACancelledPublishStillCancels()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var registration = _harness.EventListeners.Register([new CancellingListener()]);

        var publish = () => Events.PublishAsync(Event(), cts.Token);

        await publish.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ADisposedRegistrationHearsNothingMore()
    {
        var listener = new RecordingListener();
        var registration = _harness.EventListeners.Register([listener]);
        await Events.PublishAsync(Event(), CancellationToken.None);

        registration.Dispose();
        await Events.PublishAsync(Event(), CancellationToken.None);

        listener.Heard.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisteredListenersHearTheRoomsOwnEventsToo()
    {
        var listener = new RecordingListener();
        using var registration = _harness.EventListeners.Register([listener]);

        await _harness.Room.PublishRoomEventAsync(
            new AvatarPerformsActionEvent
            {
                RoomId = 1,
                ObjectId = 7,
                ActionType = AvatarActionType.Expression,
            },
            CancellationToken.None
        );

        listener
            .Heard.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<AvatarPerformsActionEvent>();
    }

    [Fact]
    public async Task WithNoRegisteredListenerNothingIsLoggedAndNothingBreaks()
    {
        Events.HasRegisteredListeners.Should().BeFalse();

        await Events.PublishAsync(Event(), CancellationToken.None);

        _log.Entries.Should().BeEmpty();
    }

    private sealed class RecordingListener : IRoomEventListener
    {
        public List<RoomEvent> Heard { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            Heard.Add(evt);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingListener(bool afterAwait = false) : IRoomEventListener
    {
        public async Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            if (afterAwait)
                await Task.Yield();

            throw new InvalidOperationException("plugin bug");
        }
    }

    private sealed class CancellingListener : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}
