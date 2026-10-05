using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public class AvatarPositionQueryTests
{
    private readonly LiveRoomHarness _harness = new(16, 16);

    [Fact]
    public async Task Player_not_in_room_has_no_position()
    {
        AddPlayer(1, 3, 4);

        Assert.Null(await _harness.Room.GetAvatarPositionAsync(2, CancellationToken.None));
        Assert.Null(await _harness.Room.GetAvatarPositionAsync(0, CancellationToken.None));
    }

    [Fact]
    public async Task Bot_is_not_returned_even_by_its_object_id()
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = 7,
            BotId = 7,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };
        bot.SetPosition(2, 2);
        Avatars()[bot.ObjectId] = bot;

        Assert.Null(await _harness.Room.GetAvatarPositionAsync(7, CancellationToken.None));
    }

    [Fact]
    public async Task Player_in_room_reports_tile_height_and_rotation()
    {
        var player = AddPlayer(1, 3, 4);
        player.SetPositionZ(new Altitude(1.5));
        player.SetRotation(Rotation.SouthWest);

        var position = await _harness.Room.GetAvatarPositionAsync(1, CancellationToken.None);

        Assert.NotNull(position);
        Assert.Equal((RoomId)1, position.RoomId);
        Assert.Equal(3, position.X);
        Assert.Equal(4, position.Y);
        Assert.Equal(1.5, position.Z.Value);
        Assert.Equal(Rotation.SouthWest, position.Rotation);
        Assert.False(position.IsIdle);
        Assert.False(position.IsWalking);
    }

    [Fact]
    public async Task Position_follows_the_avatar_after_it_steps()
    {
        var player = AddPlayer(1, 3, 4);
        player.IsWalking = true;
        player.SetPosition(4, 4);
        player.SetRotation(Rotation.East);

        var position = await _harness.Room.GetAvatarPositionAsync(1, CancellationToken.None);

        Assert.NotNull(position);
        Assert.Equal((4, 4), (position.X, position.Y));
        Assert.Equal(Rotation.East, position.Rotation);
        Assert.True(position.IsWalking);
    }

    [Fact]
    public async Task Reading_an_idle_avatar_does_not_wake_it_or_touch_its_activity()
    {
        var player = AddPlayer(1, 3, 4);
        player.Touch(1234);
        player.SetIdle(true);

        var position = await _harness.Room.GetAvatarPositionAsync(1, CancellationToken.None);

        Assert.NotNull(position);
        Assert.True(position.IsIdle);
        Assert.True(player.IsIdle);
        Assert.Equal(1234, player.LastActiveAtMs);
    }

    private IDictionary<RoomObjectId, IRoomAvatar> Avatars() =>
        (IDictionary<RoomObjectId, IRoomAvatar>)
            RoomHarness.GetMember(_harness.State, "AvatarsByObjectId")!;

    private RoomPlayerAvatar AddPlayer(int id, int x, int y)
    {
        var player = new RoomPlayerAvatar { ObjectId = id, PlayerId = id };
        player.SetPosition(x, y);
        Avatars()[player.ObjectId] = player;
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(_harness.State, "AvatarsByPlayerId")!
        )[id] = id;
        return player;
    }
}
