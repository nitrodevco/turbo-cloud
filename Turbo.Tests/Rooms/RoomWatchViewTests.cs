using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A watcher's first view of a room (docs/plugin-room-watch.md): the packets of a room entry that
/// draw the room, in the entry's order, without those only someone standing in it is told; and
/// the number of the last publish it shows, which is what keeps the room's deltas from being
/// shown twice or lost (PresenceRoomWatchTests).
/// </summary>
public sealed class RoomWatchViewTests
{
    private const int OWNER = 1;
    private const int WATCHER = 77;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomWatchViewTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };
    }

    [Fact]
    public async Task The_view_is_a_room_entry_less_what_only_someone_in_the_room_is_told()
    {
        // Someone dancing with an effect on, someone asleep: the entry replays each.
        var dancer = _room.Enter(11, 1, 1);
        dancer.SetDance(AvatarDanceType.Dance);
        dancer.SetEffect(5);
        _room.Enter(12, 2, 2).SetIdle(true);

        var room = _room.Harness.Room;
        var roomView = await room.GetEntryViewAsync((PlayerId)OWNER, Ct);
        var tail = await EnterAsync(OWNER);
        tail.Should().NotBeEmpty();
        var entry = RoomEntryComposers
            .RoomOf(_room.Harness.State.RoomId, roomView, room._roomConfig)
            .Concat(tail.Skip(1)); // the tail opens with the player's own Users
        var watched = await room.GetWatchViewAsync((PlayerId)WATCHER, Ct);

        Type[] forSomeoneInTheRoom =
        [
            typeof(RoomEntryInfoMessageComposer),
            typeof(RoomEventMessageComposer),
            typeof(WiredEnvironmentMessageComposer),
            typeof(YouAreControllerMessageComposer),
            typeof(WiredPermissionsEventMessageComposer),
            typeof(YouAreOwnerMessageComposer),
            typeof(RoomRatingMessageComposer),
            typeof(RaidProtectionCapabilityMessageComposer),
        ];

        watched
            .Composers.Select(x => x.GetType())
            .Should()
            .Equal(entry.Select(x => x.GetType()).Where(x => !forSomeoneInTheRoom.Contains(x)));
        watched.Composers.Should().ContainSingle(x => x is DanceMessageComposer);
        watched.Composers.Should().ContainSingle(x => x is AvatarEffectMessageComposer);
        watched.Composers.Should().ContainSingle(x => x is SleepMessageComposer);
        watched
            .Composers.OfType<UserUpdateMessageComposer>()
            .Single()
            .Avatars.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo(
                watched
                    .Composers.OfType<UsersMessageComposer>()
                    .First()
                    .Avatars.Select(x => x.ObjectId),
                "everyone's status, the watcher having no avatar of their own"
            );
    }

    [Fact]
    public async Task The_view_names_the_last_publish_it_shows()
    {
        var room = _room.Harness.Room;

        await room.SendComposerToRoomAsync(new RoomRatingMessageComposer(), Ct);
        await room.SendComposerToRoomAsync(new RoomRatingMessageComposer(), Ct);
        var view = await room.GetWatchViewAsync((PlayerId)WATCHER, Ct);
        await room.SendComposerToRoomAsync(new RoomRatingMessageComposer(), Ct);

        var sequences = _room.Harness.Fakes.RoomPublishes().Select(x => x.Sequence).ToList();

        sequences.Should().HaveCount(3).And.BeInAscendingOrder().And.OnlyHaveUniqueItems();
        view.Sequence.Should().Be(sequences[1]);
        sequences[2].Should().BeGreaterThan(view.Sequence);
    }

    [Fact]
    public async Task Watchers_starting_together_are_sent_the_same_packets_until_the_room_changes()
    {
        // One instance for every watcher is what lets the encoder serialize each once.
        _room.Enter(1, 1, 1);
        var room = _room.Harness.Room;

        var first = await room.GetWatchViewAsync((PlayerId)WATCHER, Ct);
        var second = await room.GetWatchViewAsync((PlayerId)(WATCHER + 1), Ct);

        second.Composers.Should().Equal(first.Composers, ReferenceEquals);

        await room.SendComposerToRoomAsync(new RoomRatingMessageComposer(), Ct);
        var afterPublish = await room.GetWatchViewAsync((PlayerId)WATCHER, Ct);

        afterPublish
            .Composers.Should()
            .NotContain(x => first.Composers.Any(y => ReferenceEquals(x, y)));

        _room.Harness.Clock.AdvanceMs(room._roomConfig.RoomTickMs);
        var afterTick = await room.GetWatchViewAsync((PlayerId)WATCHER, Ct);

        afterTick
            .Composers.Should()
            .NotContain(x => afterPublish.Composers.Any(y => ReferenceEquals(x, y)));
    }

    private async Task<IReadOnlyList<IComposer>> EnterAsync(int playerId) =>
        await _room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)playerId, _room.Harness.State.RoomId),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)playerId,
                Name = "player",
                Motto = string.Empty,
                Figure = string.Empty,
                Gender = default,
                AchievementScore = 0,
                BadgesRank = 0,
                IsOnline = true,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                RespectPoints = 0,
                RespectsLeft = 0,
                PetRespectsLeft = 0,
                RespectReplenishesLeft = 0,
            },
            new RoomEntrySnapshot { Method = RoomEntryMethodType.Default, TeleportId = 0 },
            Ct
        );
}
