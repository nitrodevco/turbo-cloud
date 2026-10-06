using System.Collections.Generic;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a plugin hears about an avatar: a tile change once per change, and the idle flag only
/// when it flips. Driven through the same module and tick-system methods the room runs.
/// </summary>
public sealed class AvatarObserverEventsTests
{
    private readonly LiveRoomHarness _harness = new(10, 10);
    private readonly Recorder _heard = new();
    private readonly RoomPlayerAvatar _avatar = new() { ObjectId = 5, PlayerId = 9 };

    public AvatarObserverEventsTests()
    {
        _harness.EventListeners.Register([_heard]);
        _avatar.SetPosition(2, 2);
        ((IDictionary<RoomObjectId, IRoomAvatar>)Member("AvatarsByObjectId"))[5] = _avatar;
        ((IDictionary<PlayerId, RoomObjectId>)Member("AvatarsByPlayerId"))[9] = 5;
        _harness.Module<RoomMapModule>().AddAvatar(_avatar, false);
    }

    private object Member(string name) => RoomHarness.GetMember(_harness.State, name)!;

    private RoomAvatarModule Avatars => _harness.Module<RoomAvatarModule>();

    private RoomMapModule Map => _harness.Module<RoomMapModule>();

    private IEnumerable<T> Heard<T>()
        where T : RoomEvent => _heard.Events.OfType<T>();

    [Fact]
    public async Task AWalkedStepIsHeardOnceWithBothTilesAndThePlayer()
    {
        _avatar.NextTileId = Map.ToIdx(3, 2);

        await Avatars.ProcessNextAvatarStepAsync(_avatar, CancellationToken.None);
        await Avatars.ProcessNextAvatarStepAsync(_avatar, CancellationToken.None);

        var moved = Heard<AvatarMovedEvent>().Should().ContainSingle().Subject;
        (moved.FromX, moved.FromY, moved.ToX, moved.ToY).Should().Be((2, 2, 3, 2));
        moved.ObjectId.Should().Be(5);
        moved.RoomId.Should().Be((RoomId)1);
        moved.CausedBy.PlayerId.Should().Be(9);
    }

    [Fact]
    public async Task AStepOntoTheTileTheAvatarStandsOnIsNotHeard()
    {
        _avatar.NextTileId = Map.ToIdx(2, 2);

        await Avatars.ProcessNextAvatarStepAsync(_avatar, CancellationToken.None);

        _heard.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ATickWithNoStepToCommitSaysNothing()
    {
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(10_000, CancellationToken.None);

        _heard.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ATeleportIsHeardAndTeleportingToTheSameTileIsNot()
    {
        await Avatars.RelocateAvatarAsync(_avatar, Map.ToIdx(2, 2), CancellationToken.None);
        _heard.Events.Should().BeEmpty();

        await Avatars.RelocateAvatarAsync(_avatar, Map.ToIdx(7, 4), CancellationToken.None);

        var moved = Heard<AvatarMovedEvent>().Should().ContainSingle().Subject;
        (moved.FromX, moved.FromY, moved.ToX, moved.ToY).Should().Be((2, 2, 7, 4));
    }

    [Fact]
    public async Task AFallingAsleepIsHeardOnceAndAnAlreadySleepingAvatarIsNot()
    {
        SleepAfterOneSecond();
        _avatar.Touch(1);

        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(60_000, CancellationToken.None);
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(120_000, CancellationToken.None);

        Heard<AvatarIdleChangedEvent>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<AvatarIdleChangedEvent>(e =>
                e.IsIdle && e.ObjectId == 5 && e.CausedBy.PlayerId == 9
            );
    }

    [Fact]
    public async Task AnActiveAvatarNeverFallsAsleepAndSaysNothing()
    {
        SleepAfterOneSecond();
        _avatar.Touch(59_500);

        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(60_000, CancellationToken.None);

        _heard.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task WakingASleepingAvatarIsHeardAndTouchingAnAwakeOneIsNot()
    {
        SleepAfterOneSecond();
        _avatar.Touch(1);
        await _harness.Room.AvatarTickSystem.ProcessAvatarsAsync(60_000, CancellationToken.None);

        Avatars.TouchAvatar(9, 61_000);
        Avatars.TouchAvatar(9, 62_000);

        Heard<AvatarIdleChangedEvent>().Select(e => e.IsIdle).Should().Equal(true, false);
    }

    [Fact]
    public async Task TheIdleExpressionPutsTheAvatarToSleepAndIsHeardOnce()
    {
        await Avatars.SetAvatarExpressionAsync(
            5,
            AvatarExpressionType.Idle,
            CancellationToken.None
        );
        await Avatars.SetAvatarExpressionAsync(
            5,
            AvatarExpressionType.Idle,
            CancellationToken.None
        );

        Heard<AvatarIdleChangedEvent>().Should().ContainSingle().Which.IsIdle.Should().BeTrue();
    }

    [Fact]
    public async Task WithNoListenerRegisteredNoObserverEventIsBuiltOrQueued()
    {
        var quiet = new LiveRoomHarness(10, 10);
        quiet.Module<RoomEventModule>().HasRegisteredListeners.Should().BeFalse();
        var avatar = new RoomPlayerAvatar { ObjectId = 5, PlayerId = 9 };
        avatar.SetPosition(2, 2);
        quiet.Module<RoomMapModule>().AddAvatar(avatar, false);
        avatar.NextTileId = quiet.Module<RoomMapModule>().ToIdx(3, 2);

        var step = () =>
            quiet
                .Module<RoomAvatarModule>()
                .ProcessNextAvatarStepAsync(avatar, CancellationToken.None);

        await step.Should().NotThrowAsync();
        avatar.X.Should().Be(3);
    }

    private void SleepAfterOneSecond()
    {
        var snapshot = RoomHarness.GetMember(_harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "IdleSleepEnabled", true);
        RoomHarness.SetMember(snapshot, "IdleSleepTimeoutSeconds", 1);
    }

    private sealed class Recorder : IRoomEventListener
    {
        public List<RoomEvent> Events { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            Events.Add(evt);

            return Task.CompletedTask;
        }
    }
}
