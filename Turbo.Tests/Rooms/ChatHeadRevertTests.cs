using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(
        20,
        20,
        new RoomConfig { ChatFloodWindowMs = 0, ChatlogEnabled = false }
    );

    public ChatHeadRevertTests()
    {
        _room.Harness.Room.UseEventSystem(new TestEventBus().System);
        _room.Harness.Room.UseCommands(
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
        _room.Enter(SPEAKER, 7, 7, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 3, 5, "listener", Rotation.East);
        var start = Now();

        var said = await SayAsync(chatType, "listener");
        await TickAsync(start + 4_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.SouthEast, Rotation.East));

        // The room's clock is the test's, so the edge is exact: a millisecond short of 5 s the
        // head still looks at the speaker, and at 5 s it is back on the body.
        await TickAsync(said + 4_999);

        listener.HeadRotation.Should().Be(Rotation.SouthEast);
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(1);

        await TickAsync(said + 5_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.East, Rotation.East));
        listener.HeadRotation.Should().Be(Rotation.East);
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_whisper_turns_its_recipient_only()
    {
        _room.Enter(SPEAKER, 7, 7, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 3, 5, "listener", Rotation.East);
        var bystander = _room.Enter(BYSTANDER, 3, 7, "bystander", Rotation.East);

        await SayAsync(RoomChatType.Whisper, "listener");

        listener.HeadRotation.Should().Be(Rotation.SouthEast);
        bystander.HeadRotation.Should().Be(Rotation.East);
    }

    [Fact]
    public async Task A_head_already_facing_the_speaker_gets_only_the_look_back()
    {
        // The recorded positions: the listener sits on the floor facing north, its head still
        // north-west from a look at the door, where the speaker talks.
        _room.Enter(SPEAKER, 3, 5, "speaker", Rotation.East);
        var listener = _room.Enter(LISTENER, 7, 7, "listener", Rotation.North);
        listener.Sit(true, rot: Rotation.North);
        listener.SetHeadRotation(Rotation.NorthWest);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);

        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before);

        await TickAsync(said + 5_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.North, Rotation.North));
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task A_look_back_to_a_head_already_on_the_body_sends_nothing()
    {
        // The recorded positions: the listener faces south, the speaker one tile west of its line
        // of sight; a look straight ahead puts the head back on the body before the time is up.
        _room.Enter(SPEAKER, 5, 8, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 6, 2, "listener", Rotation.South);
        var start = Now();

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        listener.HeadRotation.Should().Be(Rotation.SouthWest);

        await _room.Harness.Room.LookToAsync(
            _room.ContextFor(LISTENER),
            6,
            8,
            CancellationToken.None
        );
        await TickAsync(start + 2_500);
        LastUpdateOf(LISTENER).Should().Be((Rotation.South, Rotation.South));
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        await TickAsync(said + 5_000);

        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_sleeping_listener_neither_turns_nor_wakes()
    {
        _room.Enter(SPEAKER, 5, 8, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 6, 2, "listener", Rotation.South);
        listener.SetIdle(true);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_000);

        listener.HeadRotation.Should().Be(Rotation.South);
        listener.IsIdle.Should().BeTrue();
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_look_back_still_comes_when_the_listener_falls_asleep()
    {
        _room.Enter(SPEAKER, 5, 8, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 6, 2, "listener", Rotation.South);
        var start = Now();

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        listener.SetIdle(true);
        await TickAsync(said + 5_000);

        LastUpdateOf(LISTENER).Should().Be((Rotation.South, Rotation.South));
    }

    [Fact]
    public async Task A_listener_behind_the_speaker_neither_turns_nor_gets_an_update()
    {
        // The recorded positions: the listener east of the speaker, facing away from it.
        _room.Enter(SPEAKER, 3, 5, "speaker", Rotation.East);
        var listener = _room.Enter(LISTENER, 8, 7, "listener", Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 4_000);
        await TickAsync(said + 5_000);

        listener.HeadRotation.Should().Be(Rotation.East);
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before);
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
        var listener = _room.Enter(LISTENER, 6, 6, "listener", body);
        _room.Enter(SPEAKER, 6 + dx, 6 + dy, "speaker", Rotation.North);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        listener.HeadRotation.Should().Be(expected ?? body);
        listener.Rotation.Should().Be(body);
        _room
            .Harness.Fakes.AvatarUpdates()
            .Should()
            .HaveCount(expected is null ? before : before + 1);
    }

    [Fact]
    public async Task A_floor_sitter_turns_its_head_and_keeps_its_sit()
    {
        _room.Enter(SPEAKER, 6, 7, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 6, 6, "listener", Rotation.East);
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
        _room.Enter(SPEAKER, speakerX, speakerY, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 5, 6, "listener", Rotation.East);
        if (seat == "chair")
            listener.Sit(true, rot: Rotation.East);
        else
            listener.Lay(true, rot: Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before + 1);
        LastUpdateOf(LISTENER).Should().Be((Rotation.East, Rotation.East));
        listener
            .HasStatus(seat == "chair" ? AvatarStatusType.Sit : AvatarStatusType.Lay)
            .Should()
            .BeTrue();

        await TickAsync(said + 5_000);
        await TickAsync(said + 7_000);

        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before + 1);
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
        _room.Enter(SPEAKER, 8 + dx, 8 + dy, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 8, 8, "listener", body);
        if (seat == "chair")
            listener.Sit(true, rot: body);
        else
            listener.Lay(true, rot: body);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_000);

        listener.HeadRotation.Should().Be(body);
        listener.Rotation.Should().Be(body);
        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(updated ? before + 1 : before);
    }

    [Fact]
    public async Task A_listener_held_by_a_seat_gets_nothing_where_it_would_not_turn()
    {
        // The recorded positions: the speaker 2 east 1 north of the chair, ahead of it (2/2).
        _room.AddFloorItem(50, 5, 6, canSit: true).SetRotation(Rotation.East);
        _room.Enter(SPEAKER, 7, 5, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 5, 6, "listener", Rotation.East);
        listener.Sit(true, rot: Rotation.East);
        var start = Now();
        await TickAsync(start);
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        var said = await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);
        await TickAsync(said + 5_000);
        await TickAsync(said + 7_000);

        _room.Harness.Fakes.AvatarUpdates().Should().HaveCount(before);
    }

    [Fact]
    public async Task A_listener_on_its_last_step_turns_as_it_arrives()
    {
        // The recorded walk: the listener steps east onto 11,12 while the speaker at 11,8 talks.
        _room.Enter(SPEAKER, 11, 8, "speaker", Rotation.South);
        var listener = _room.Enter(LISTENER, 10, 12, "listener", Rotation.East);
        listener.IsWalking = true;
        listener.NextTileId = _room.Map.ToIdx(11, 12);
        var start = Now();

        await SayAsync(RoomChatType.Chat);
        await TickAsync(start + 1_000);

        var update = _room
            .Harness.Fakes.AvatarUpdates()
            .Last()
            .Should()
            .ContainSingle(a => a.ObjectId == LISTENER)
            .Subject;
        (update.X, update.Y, update.HeadRotation, update.BodyRotation)
            .Should()
            .Be((11, 12, Rotation.NorthEast, Rotation.East));
    }

    [Fact]
    public async Task A_later_line_starts_the_5_s_again()
    {
        _room.Enter(SPEAKER, 7, 7, "speaker", Rotation.North);
        var listener = _room.Enter(LISTENER, 3, 5, "listener", Rotation.East);

        await SayAsync(RoomChatType.Chat);
        _room.Clock.AdvanceMs(1_500);
        var said = await SayAsync(RoomChatType.Chat);

        // Past the first line's 5 s (3.5 s after the second), a millisecond short of the second's.
        await TickAsync(said + 4_999);
        listener.HeadRotation.Should().Be(Rotation.SouthEast);

        await TickAsync(said + 5_000);
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
        room.Harness.Room.UseEventSystem(new TestEventBus().System);
        room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
        room.Enter(SPEAKER, 13, 7, "speaker");
        var listener = room.Enter(LISTENER, 6, 6);
        listener.SetRotation(Rotation.East);

        await room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            room.ContextFor(SPEAKER),
            RoomChatType.Chat,
            "hello",
            0,
            -1,
            null,
            CancellationToken.None
        );

        listener.HeadRotation.Should().Be(Rotation.East);
    }

    /// <summary>The room's clock, which moves only when <see cref="TickAsync"/> moves it.</summary>
    private long Now() => _room.Harness.NowMs();

    /// <summary>The speaker's line, and the room's clock when it was said: the look-back counts from it.</summary>
    private async Task<long> SayAsync(RoomChatType type, string? recipient = null)
    {
        await _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            _room.ContextFor(SPEAKER),
            type,
            "hello",
            0,
            -1,
            type == RoomChatType.Whisper ? recipient : null,
            CancellationToken.None
        );

        return Now();
    }

    /// <summary>Moves the room's clock on to <paramref name="at"/> and runs the avatar tick there.</summary>
    private Task TickAsync(long at)
    {
        _room.Clock.AdvanceMs(at - Now());

        return _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(Now(), Ct);
    }

    private (Rotation Head, Rotation Body) LastUpdateOf(int objectId)
    {
        var snapshot = _room
            .Harness.Fakes.AvatarUpdates()
            .Last()
            .Should()
            .ContainSingle(a => a.ObjectId == objectId)
            .Subject;
        return (snapshot.HeadRotation, snapshot.BodyRotation);
    }
}
