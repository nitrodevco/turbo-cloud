using System.Diagnostics;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A listener's head turned toward someone talking, as Habbo does it (official client capture,
/// hhus 2026-10-17, room 80463631): on the next update the listener's head faces the speaker and
/// its body stays; exactly ~5.0 s after that the head looks back to the body (3/3: talk, talk,
/// whisper to it). A listener held by a seat keeps its head, yet still gets the update 5 s later
/// (1/1); one behind the speaker gets neither (3/3). Turbo turned the head and left it there, and
/// a whisper turned nobody.
/// </summary>
public sealed class ChatHeadRevertTests
{
    private const int SPEAKER = 1;
    private const int LISTENER = 2;
    private const int BYSTANDER = 3;

    private readonly WiredRoom _room = new(
        16,
        16,
        new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = false }
    );

    public ChatHeadRevertTests()
    {
        RoomHarness.SetField(_room.Harness.Room, "_eventSystem", new TestEventBus().System);
        RoomHarness.SetField(
            _room.Harness.Room,
            "_commandRegistryProvider",
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
    }

    [Theory]
    [InlineData(RoomChatType.Chat)]
    [InlineData(RoomChatType.Whisper)]
    public async Task A_listener_looks_at_the_speaker_and_back_to_its_body_5_s_later(
        RoomChatType chatType
    )
    {
        // The recorded positions: the listener at the door facing east, the speaker on a chair.
        Enter(SPEAKER, "speaker", 7, 7, Rotation.North);
        var listener = Enter(LISTENER, "listener", 3, 5, Rotation.East);
        var start = Now();

        await SayAsync(chatType, "listener");
        await TickAsync(start + 4_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.SouthEast, Rotation.East));

        await TickAsync(start + 5_600);

        LastUpdateOf(LISTENER).Should().Be((Rotation.East, Rotation.East));
        listener.HeadRotation.Should().Be(Rotation.East);
        Updates().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_whisper_turns_its_recipient_only()
    {
        Enter(SPEAKER, "speaker", 7, 7, Rotation.North);
        var listener = Enter(LISTENER, "listener", 3, 5, Rotation.East);
        var bystander = Enter(BYSTANDER, "bystander", 3, 7, Rotation.East);

        await SayAsync(RoomChatType.Whisper, "listener");

        listener.HeadRotation.Should().Be(Rotation.SouthEast);
        bystander.HeadRotation.Should().Be(Rotation.East);
    }

    [Fact]
    public async Task A_listener_held_by_a_seat_keeps_its_head_and_still_gets_the_update_5_s_later()
    {
        // The recorded positions: the listener on a chair facing north, the speaker at the door.
        _room.AddFloorItem(10, 7, 7, canSit: true);
        Enter(SPEAKER, "speaker", 3, 5, Rotation.East);
        var listener = Enter(LISTENER, "listener", 7, 7, Rotation.North);
        listener.Sit(true, rot: Rotation.North);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);

        listener.HeadRotation.Should().Be(Rotation.North);
        Updates().Should().HaveCount(before);

        await TickAsync(start + 5_600);

        LastUpdateOf(LISTENER).Should().Be((Rotation.North, Rotation.North));
        Updates().Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task A_listener_behind_the_speaker_neither_turns_nor_gets_an_update()
    {
        // The recorded positions: the listener east of the speaker, facing away from it.
        Enter(SPEAKER, "speaker", 3, 5, Rotation.East);
        var listener = Enter(LISTENER, "listener", 8, 7, Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);
        await TickAsync(start + 5_600);

        listener.HeadRotation.Should().Be(Rotation.East);
        Updates().Should().HaveCount(before);
    }

    private RoomPlayerAvatar Enter(int id, string name, int x, int y, Rotation body)
    {
        var avatar = _room.Enter(id, x, y);
        RoomHarness.SetMember(avatar, "Name", name);
        avatar.SetRotation(body);
        return avatar;
    }

    /// <summary>The room's clock, as the room grain reads it.</summary>
    private static long Now() => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    private Task SayAsync(RoomChatType type, string? recipient = null) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(100 + SPEAKER, _room.Harness.Room.RoomId),
            type,
            "hello",
            0,
            -1,
            type == RoomChatType.Whisper ? recipient : null,
            CancellationToken.None
        );

    private Task TickAsync(long now) =>
        _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, CancellationToken.None);

    private List<RoomAvatarSnapshot[]> Updates() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("OnNextAsync")
                .Select(c => (RoomOutboundSnapshot)c.Args[0]!)
                .SelectMany(x => x.Composers.OfType<UserUpdateMessageComposer>())
                .Select(x => x.Avatars.ToArray()),
        ];

    private (Rotation Head, Rotation Body) LastUpdateOf(int objectId)
    {
        var snapshot = Updates().Last().Should().ContainSingle(a => a.ObjectId == objectId).Subject;
        return (snapshot.HeadRotation, snapshot.BodyRotation);
    }
}
