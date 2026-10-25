using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User says keyword" with "hide" ticked, as Habbo runs it (hhus, 2026-10-13, a box set to match
/// all text): the line is not shown to the room; its speaker alone gets it back as a whisper over
/// their own avatar - Chat and Shout alike. The checkbox used to be stored but nothing read it,
/// so every hidden line went to the whole room.
/// </summary>
public sealed class WiredSaysKeywordHideTests
{
    private const int SPEAKER = 3;

    private readonly WiredRoom _room = new(8, 8);

    public WiredSaysKeywordHideTests()
    {
        // The chat path publishes every line and matches commands first, as CommandRoomFixture sets up.
        _room.Harness.Room.UseEventSystem(new TestEventBus().System);
        _room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
    }

    [Theory]
    [InlineData(RoomChatType.Chat)]
    [InlineData(RoomChatType.Shout)]
    public async Task A_hidden_line_is_whispered_back_to_its_speaker_and_not_shown_to_the_room(
        RoomChatType type
    )
    {
        await AddSaysKeywordAsync(hide: true);
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("parity hello", type);

        ToRoom().Should().BeEmpty();
        var echo = WhispersTo(100 + SPEAKER).Should().ContainSingle().Subject;
        echo.Text.Should().Be("parity hello");
        echo.ObjectId.Value.Should().Be(SPEAKER);
        echo.ReceiverRoomIndex.Should().Be(SPEAKER);
    }

    [Fact]
    public async Task Without_hide_the_line_goes_to_the_room_as_before()
    {
        await AddSaysKeywordAsync(hide: false);
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("parity hello", RoomChatType.Chat);

        ToRoom().Select(c => c.Text).Should().Equal("parity hello");
        WhispersTo(100 + SPEAKER).Should().BeEmpty();
    }

    // trigger.0.R1c (official client capture 2026-10-25T03-16-09, LAB-W3 wlab9): a line the word
    // filter changes for the room still fires User Says Keyword with what was typed. Ours read the
    // filtered line, so "secret" never matched once the room filtered it.
    [Fact]
    public async Task A_filtered_line_fires_the_keyword_as_it_was_typed()
    {
        _room.Harness.State.FilterWords.Add("secret");
        _room.Harness.State.IsFilterLoaded = true;
        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                intParams: [0, 0, 0],
                stringParam: "my secret"
            )
        )
            .Should()
            .BeTrue();
        _room.AddBox<WiredActionShowMessage>(2, 0, 0, "wf_act_show_message");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [1, 34, -1],
                stringParam: "heard",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        _room.Enter(SPEAKER, 2, 2);
        var wired = _room.Harness.Module<RoomWiredSystem>();
        await wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            CancellationToken.None
        );
        await wired.ProcessWiredAsync(11_000, dormant: false, CancellationToken.None);

        await SayAsync("my secret", RoomChatType.Chat);
        await wired.ProcessWiredAsync(12_000, dormant: false, CancellationToken.None);
        await wired.ProcessWiredAsync(13_000, dormant: false, CancellationToken.None);

        ToRoom().First().Text.Should().Be("my bobba");
        _room
            .Harness.Fakes.Sent<ChatMessageComposer>()
            .Select(c => c.Text)
            .Should()
            .Contain("heard");
    }

    private async Task AddSaysKeywordAsync(bool hide)
    {
        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");

        // Habbo's own box: not owner only, match all text, hide as given.
        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 2, hide ? 1 : 0]))
            .Should()
            .BeTrue();
    }

    private Task SayAsync(string text, RoomChatType type) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            _room.ContextFor(SPEAKER),
            type,
            text,
            0,
            -1,
            null,
            CancellationToken.None
        );

    private IEnumerable<ChatMessageComposer> ToRoom() =>
        _room
            .Harness.Fakes.SentToRoom<ChatMessageComposer>()
            .Where(c => c is not WhisperMessageComposer);

    private IEnumerable<WhisperMessageComposer> WhispersTo(int playerId) =>
        _room.Harness.Fakes.SentToPlayer<WhisperMessageComposer>(playerId);
}
