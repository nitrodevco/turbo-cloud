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
/// whisper to it), counted again from a later line, and only when it changes the head (2/2); one
/// behind the speaker or asleep gets neither (3/3, 2/2). Turbo turned
/// the head and left it there, a whisper turned nobody, and its quadrant rule turned heads Habbo
/// keeps ahead and kept heads Habbo turns (a speaker 90 degrees aside, 6+ tiles away).
/// </summary>
public sealed class ChatHeadRevertTests
{
    private const int SPEAKER = 1;
    private const int LISTENER = 2;
    private const int BYSTANDER = 3;

    private readonly WiredRoom _room = new(
        20,
        20,
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

        var said = await SayAsync(chatType, "listener");
        await TickAsync(start + 4_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.SouthEast, Rotation.East));

        await TickAsync(said + 5_600);

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
    public async Task A_head_already_facing_the_speaker_gets_only_the_look_back()
    {
        // The recorded positions: the listener sits on the floor facing north, its head still
        // north-west from a look at the door, where the speaker talks.
        Enter(SPEAKER, "speaker", 3, 5, Rotation.East);
        var listener = Enter(LISTENER, "listener", 7, 7, Rotation.North);
        listener.Sit(true, rot: Rotation.North);
        listener.SetHeadRotation(Rotation.NorthWest);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);

        Updates().Should().HaveCount(before);

        await TickAsync(said + 5_600);

        LastUpdateOf(LISTENER).Should().Be((Rotation.North, Rotation.North));
        Updates().Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task A_look_back_to_a_head_already_on_the_body_sends_nothing()
    {
        // The recorded positions: the listener faces south, the speaker one tile west of its line
        // of sight; a look straight ahead puts the head back on the body before the time is up.
        Enter(SPEAKER, "speaker", 5, 8, Rotation.North);
        var listener = Enter(LISTENER, "listener", 6, 2, Rotation.South);
        var start = Now();

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        listener.HeadRotation.Should().Be(Rotation.SouthWest);

        await _room.Harness.Room.LookToAsync(
            ActionContext.CreateForPlayer(100 + LISTENER, _room.Harness.Room.RoomId),
            6,
            8,
            CancellationToken.None
        );
        await TickAsync(start + 2_500);
        LastUpdateOf(LISTENER).Should().Be((Rotation.South, Rotation.South));
        var before = Updates().Count;

        await TickAsync(said + 5_600);

        Updates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_sleeping_listener_neither_turns_nor_wakes()
    {
        Enter(SPEAKER, "speaker", 5, 8, Rotation.North);
        var listener = Enter(LISTENER, "listener", 6, 2, Rotation.South);
        listener.SetIdle(true);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_600);

        listener.HeadRotation.Should().Be(Rotation.South);
        listener.IsIdle.Should().BeTrue();
        Updates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_look_back_still_comes_when_the_listener_falls_asleep()
    {
        Enter(SPEAKER, "speaker", 5, 8, Rotation.North);
        var listener = Enter(LISTENER, "listener", 6, 2, Rotation.South);
        var start = Now();

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        listener.SetIdle(true);
        await TickAsync(said + 5_600);

        LastUpdateOf(LISTENER).Should().Be((Rotation.South, Rotation.South));
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

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);
        await TickAsync(said + 5_600);

        listener.HeadRotation.Should().Be(Rotation.East);
        Updates().Should().HaveCount(before);
    }

    /// <summary>
    /// Every official sample (hhus 2026-10-17, lab33 + lab34): the listener's body, where the
    /// speaker stood from it, and the head it got (null: no update at all).
    /// </summary>
    [Theory]
    [InlineData(Rotation.East, 4, 2, Rotation.SouthEast)]
    [InlineData(Rotation.East, 7, 8, Rotation.SouthEast)]
    [InlineData(Rotation.East, 6, 12, Rotation.SouthEast)]
    [InlineData(Rotation.East, 0, -4, Rotation.NorthEast)]
    [InlineData(Rotation.East, 0, 1, Rotation.SouthEast)]
    [InlineData(Rotation.East, -3, -3, null)]
    [InlineData(Rotation.East, -5, -2, null)]
    [InlineData(Rotation.South, 7, 11, null)]
    [InlineData(Rotation.South, 2, 3, null)]
    [InlineData(Rotation.South, 3, 3, Rotation.SouthEast)]
    [InlineData(Rotation.South, 4, 3, Rotation.SouthEast)]
    [InlineData(Rotation.West, 1, -5, null)]
    public async Task A_listener_turns_as_Habbo_turned_it_for_each_recorded_speaker_position(
        Rotation body,
        int dx,
        int dy,
        Rotation? expected
    )
    {
        var listener = Enter(LISTENER, "listener", 6, 6, body);
        Enter(SPEAKER, "speaker", 6 + dx, 6 + dy, Rotation.North);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        listener.HeadRotation.Should().Be(expected ?? body);
        listener.Rotation.Should().Be(body);
        Updates().Should().HaveCount(expected is null ? before : before + 1);
    }

    [Fact]
    public async Task A_floor_sitter_turns_its_head_and_keeps_its_sit()
    {
        Enter(SPEAKER, "speaker", 6, 7, Rotation.North);
        var listener = Enter(LISTENER, "listener", 6, 6, Rotation.East);
        listener.Sit(true, rot: Rotation.East);

        await SayAsync(RoomChatType.Chat);

        listener.HeadRotation.Should().Be(Rotation.SouthEast);
        listener.Rotation.Should().Be(Rotation.East);
        listener.HasStatus(AvatarStatusType.Sit).Should().BeTrue();
    }

    [Theory]
    // The recorded positions: a chair at 5,6 facing east, the speaker 2 east 1 south (2/2) and
    // 1 east 2 north (1/1), where a standing listener would turn its head.
    [InlineData("chair", 7, 7)]
    [InlineData("chair", 6, 4)]
    // A bed holds its listener as a chair does (inferred, not recorded).
    [InlineData("bed", 7, 7)]
    public async Task A_listener_held_by_a_seat_gets_one_unchanged_update_and_no_look_back(
        string seat,
        int speakerX,
        int speakerY
    )
    {
        _room
            .AddFloorItem(50, 5, 6, canSit: seat == "chair", canLay: seat == "bed")
            .SetRotation(Rotation.East);
        Enter(SPEAKER, "speaker", speakerX, speakerY, Rotation.North);
        var listener = Enter(LISTENER, "listener", 5, 6, Rotation.East);
        if (seat == "chair")
            listener.Sit(true, rot: Rotation.East);
        else
            listener.Lay(true, rot: Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        Updates().Should().HaveCount(before + 1);
        LastUpdateOf(LISTENER).Should().Be((Rotation.East, Rotation.East));
        listener
            .HasStatus(seat == "chair" ? AvatarStatusType.Sit : AvatarStatusType.Lay)
            .Should()
            .BeTrue();

        await TickAsync(said + 5_600);
        await TickAsync(said + 7_000);

        Updates().Should().HaveCount(before + 1);
    }

    /// <summary>
    /// Every line of the seated and lying samples (official client capture, 2026-10-18, lab52 Q3:
    /// a chair facing south, a bed facing east, 10 speaker positions): the head and body never
    /// change; where a standing listener would turn, one unchanged update, else none.
    /// </summary>
    [Theory]
    [InlineData("chair", Rotation.South, 0, -2, false)]
    [InlineData("chair", Rotation.South, -2, -2, false)]
    [InlineData("chair", Rotation.South, -3, 0, true)]
    [InlineData("chair", Rotation.South, -2, 2, true)]
    [InlineData("chair", Rotation.South, -1, 3, true)]
    [InlineData("bed", Rotation.East, 4, 0, false)]
    [InlineData("bed", Rotation.East, 3, 2, true)]
    [InlineData("bed", Rotation.East, 0, 2, true)]
    [InlineData("bed", Rotation.East, 3, -2, false)]
    [InlineData("bed", Rotation.East, 0, -1, true)]
    public async Task A_seated_or_lying_listener_keeps_head_and_body_for_each_recorded_speaker_position(
        string seat,
        Rotation body,
        int dx,
        int dy,
        bool updated
    )
    {
        _room
            .AddFloorItem(50, 8, 8, canSit: seat == "chair", canLay: seat == "bed")
            .SetRotation(body);
        Enter(SPEAKER, "speaker", 8 + dx, 8 + dy, Rotation.North);
        var listener = Enter(LISTENER, "listener", 8, 8, body);
        if (seat == "chair")
            listener.Sit(true, rot: body);
        else
            listener.Lay(true, rot: body);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_600);

        listener.HeadRotation.Should().Be(body);
        listener.Rotation.Should().Be(body);
        Updates().Should().HaveCount(updated ? before + 1 : before);
    }

    [Fact]
    public async Task A_listener_held_by_a_seat_gets_nothing_where_it_would_not_turn()
    {
        // The recorded positions: the speaker 2 east 1 north of the chair, ahead of it (2/2).
        _room.AddFloorItem(50, 5, 6, canSit: true).SetRotation(Rotation.East);
        Enter(SPEAKER, "speaker", 7, 5, Rotation.North);
        var listener = Enter(LISTENER, "listener", 5, 6, Rotation.East);
        listener.Sit(true, rot: Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = Updates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_600);
        await TickAsync(said + 7_000);

        Updates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_listener_on_its_last_step_turns_as_it_arrives()
    {
        // The recorded walk: the listener steps east onto 11,12 while the speaker at 11,8 talks.
        Enter(SPEAKER, "speaker", 11, 8, Rotation.South);
        var listener = Enter(LISTENER, "listener", 10, 12, Rotation.East);
        listener.IsWalking = true;
        listener.NextTileId = _room.Map.ToIdx(11, 12);
        var start = Now();

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        var update = Updates().Last().Should().ContainSingle(a => a.ObjectId == LISTENER).Subject;
        (update.X, update.Y, update.HeadRotation, update.BodyRotation)
            .Should()
            .Be((11, 12, Rotation.NorthEast, Rotation.East));
    }

    [Fact]
    public async Task A_later_line_starts_the_5_s_again()
    {
        Enter(SPEAKER, "speaker", 7, 7, Rotation.North);
        var listener = Enter(LISTENER, "listener", 3, 5, Rotation.East);

        await SayAsync(RoomChatType.Chat);
        await Task.Delay(1_500);
        var second = Now();
        var said = await SayAsync(RoomChatType.Chat);

        // Past the first line's 5 s (at most 3.5 s after the second began), short of the second's.
        await TickAsync(second + 4_000);
        listener.HeadRotation.Should().Be(Rotation.SouthEast);

        await TickAsync(said + 5_600);
        listener.HeadRotation.Should().Be(Rotation.East);
    }

    [Fact]
    public async Task A_configured_range_keeps_listeners_further_away_still()
    {
        var room = new WiredRoom(
            20,
            20,
            new RoomConfig
            {
                ChatFloodWindowMs = 0,
                ChatlogEnabled = false,
                ChatLookAtRange = 6,
            }
        );
        RoomHarness.SetField(room.Harness.Room, "_eventSystem", new TestEventBus().System);
        RoomHarness.SetField(
            room.Harness.Room,
            "_commandRegistryProvider",
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
        RoomHarness.SetMember(room.Enter(SPEAKER, 13, 7), "Name", "speaker");
        var listener = room.Enter(LISTENER, 6, 6);
        listener.SetRotation(Rotation.East);

        await room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(100 + SPEAKER, room.Harness.Room.RoomId),
            RoomChatType.Chat,
            "hello",
            0,
            -1,
            null,
            CancellationToken.None
        );

        listener.HeadRotation.Should().Be(Rotation.East);
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

    /// <summary>
    /// The speaker's line, and the room's clock once it is said: the room stamps the look-back
    /// between a <see cref="Now"/> taken before and this, however long a loaded machine took.
    /// </summary>
    private async Task<long> SayAsync(RoomChatType type, string? recipient = null)
    {
        await _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(100 + SPEAKER, _room.Harness.Room.RoomId),
            type,
            "hello",
            0,
            -1,
            type == RoomChatType.Whisper ? recipient : null,
            CancellationToken.None
        );

        return Now();
    }

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
