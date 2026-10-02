using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public class ChatHeadDirectionTests
{
    private static readonly (int X, int Y)[] SpeakerOffsets =
    [
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
    ];

    public static IEnumerable<object[]> Directions()
    {
        for (var body = 0; body < SpeakerOffsets.Length; body++)
        for (var target = 0; target < SpeakerOffsets.Length; target++)
            yield return [(Rotation)body, (Rotation)target];
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Chat_turns_head_only_to_body_or_adjacent_directions(
        Rotation body,
        Rotation target
    )
    {
        var fixture = new Fixture();
        var listener = fixture.AddPlayer(2, 4, 4, body);
        listener.SetHeadRotation(body.Rotate(-1));
        var offset = SpeakerOffsets[(int)target];
        fixture.AddPlayer(1, 4 + offset.X, 4 + offset.Y, Rotation.North);

        Assert.True(await fixture.ChatAsync());

        var expected =
            target == body || target == body.Rotate(-1) || target == body.Rotate(1)
                ? target
                : body.Rotate(-1);
        Assert.Equal(expected, listener.HeadRotation);
        Assert.Equal(body, listener.Rotation);
    }

    [Theory]
    [InlineData(0, 0, RoomChatType.Chat, false, Rotation.NorthWest)]
    [InlineData(1, -1, RoomChatType.Chat, true, Rotation.NorthWest)]
    [InlineData(0, -7, RoomChatType.Chat, false, Rotation.NorthWest)]
    [InlineData(0, -7, RoomChatType.Shout, false, Rotation.North)]
    [InlineData(0, 1, RoomChatType.Shout, false, Rotation.NorthWest)]
    [InlineData(1, -1, RoomChatType.Whisper, false, Rotation.NorthWest)]
    public async Task Chat_preserves_overlap_walking_range_and_whisper_rules(
        int dx,
        int dy,
        RoomChatType chatType,
        bool walking,
        Rotation expected
    )
    {
        var fixture = new Fixture();
        var listener = fixture.AddPlayer(2, 8, 8, Rotation.North);
        listener.SetHeadRotation(Rotation.NorthWest);
        listener.IsWalking = walking;
        var speaker = fixture.AddPlayer(1, 8 + dx, 8 + dy, Rotation.East);

        Assert.True(await fixture.ChatAsync(chatType));
        Assert.Equal(expected, listener.HeadRotation);
        Assert.Equal(Rotation.North, listener.Rotation);
        Assert.Equal(Rotation.East, speaker.HeadRotation);
    }

    [Fact]
    public async Task Human_bot_does_not_turn_its_head_backwards_for_chat()
    {
        var fixture = new Fixture();
        var bot = new RoomBotAvatar
        {
            ObjectId = 2,
            BotId = 2,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };
        bot.SetPosition(4, 4);
        bot.SetRotation(Rotation.North);
        fixture.AddAvatar(bot);
        fixture.AddPlayer(1, 4, 5, Rotation.North);

        Assert.True(await fixture.ChatAsync());
        Assert.Equal(Rotation.North, bot.HeadRotation);
        Assert.Equal(Rotation.North, bot.Rotation);
    }

    private sealed class Fixture
    {
        private readonly LiveRoomHarness _harness = new(16, 16);

        public Fixture()
        {
            RoomHarness.SetField(
                _harness.Room,
                "_eventSystem",
                new EventSystem(
                    new EventRegistry(_harness.Services, NullLogger<EventRegistry>.Instance)
                )
            );
            RoomHarness.SetField(
                _harness.Room,
                "_roomConfig",
                new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = false }
            );
            RoomHarness.SetMember(
                RoomHarness.GetMember(_harness.State, "RoomSnapshot")!,
                "ChatProtection",
                ChatFloodSensitivityType.Normal
            );
        }

        public RoomPlayerAvatar AddPlayer(int id, int x, int y, Rotation body)
        {
            var player = new RoomPlayerAvatar { ObjectId = id, PlayerId = id };
            RoomHarness.SetMember(player, "Name", id == 2 ? "listener" : "speaker");
            player.SetPosition(x, y);
            player.SetRotation(body);
            AddAvatar(player);
            (
                (IDictionary<PlayerId, RoomObjectId>)
                    RoomHarness.GetMember(_harness.State, "AvatarsByPlayerId")!
            )[id] = id;
            return player;
        }

        public void AddAvatar(IRoomAvatar avatar) =>
            (
                (IDictionary<RoomObjectId, IRoomAvatar>)
                    RoomHarness.GetMember(_harness.State, "AvatarsByObjectId")!
            )[avatar.ObjectId] = avatar;

        public Task<bool> ChatAsync(RoomChatType type = RoomChatType.Chat) =>
            _harness.Room.SendChatFromPlayerAsync(
                ActionContext.CreateForPlayer(1, 1),
                type,
                "hello",
                0,
                CancellationToken.None,
                recipientName: type == RoomChatType.Whisper ? "listener" : null
            );
    }
}
