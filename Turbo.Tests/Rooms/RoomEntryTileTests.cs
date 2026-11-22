using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Where people come in (<see cref="RoomMapModule.EntryX"/>): the model's door, or, when a model
/// row puts its door off the map or on a hole (the floor plan editor refuses both, a hand-written
/// or imported row does not), the open tile nearest it. The avatar is put down there, and the
/// client is told the same tile as the room's entry tile. Before, a player came in on the hole,
/// or on 0,0 for a door off the map, here a hole too.
/// </summary>
public sealed class RoomEntryTileTests
{
    private const int PLAYER = 300;

    // A wall of holes along the top and down the left side: 0,0 is a hole.
    private const string MODEL = "xxxxxx\rx00000\rx00000\rx00000\rx00000";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(2, 3, 2, 3, "a door on a tile of the model is the door")]
    [InlineData(0, 2, 1, 2, "a door on a hole moves to the open tile beside it")]
    [InlineData(9, 2, 5, 2, "a door off the map moves to the open tile nearest it")]
    public async Task People_come_in_on_the_door_or_the_open_tile_nearest_it(
        int doorX,
        int doorY,
        int entryX,
        int entryY,
        string why
    )
    {
        var room = new WiredRoom(model: Model(doorX, doorY));

        room.Harness.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        room.Harness.Fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };

        await EnterAsync(room);

        var arrived = room
            .Harness.Module<RoomAvatarModule>()
            .Players.Single(x => x.PlayerId == PLAYER);
        (arrived.X, arrived.Y).Should().Be((entryX, entryY), why);
        arrived.Rotation.Should().Be(Rotation.East, "the door's own direction is kept");

        var map = await room.Harness.Room.GetMapSnapshotAsync(Ct);
        (map.DoorX, map.DoorY).Should().Be((entryX, entryY), "the client is told the same tile");
    }

    private static RoomModelSnapshot Model(int doorX, int doorY)
    {
        var model = RoomModelProvider.CleanModelString(MODEL);
        var compiled = RoomModelProvider.CompileModelFromString(model);

        return new RoomModelSnapshot
        {
            Id = 1,
            Name = "entry_test",
            Model = model,
            DoorX = doorX,
            DoorY = doorY,
            DoorRotation = Rotation.East,
            Width = compiled.Width,
            Height = compiled.Height,
            Size = compiled.Width * compiled.Height,
            BaseHeights = compiled.Heights,
            BaseFlags = compiled.Flags,
        };
    }

    private static Task EnterAsync(WiredRoom room) =>
        room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, room.Harness.Room.RoomId),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)PLAYER,
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
            RoomEntrySnapshot.Default,
            Ct
        );
}
