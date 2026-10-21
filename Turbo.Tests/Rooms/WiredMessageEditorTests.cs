using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The message editor of WIN63-202610121624 (Show Message, Bot Talk, Bot Talk To Avatar): 400
/// characters, the placeholders of the stack in wired context section 7, <c>$(trigger_text)</c>
/// from "User Says Keyword", and the bot boxes' "hide bot name" (param 2), which reaches the
/// client as the chat message's trailing hideUsername.
/// </summary>
public sealed class WiredMessageEditorTests
{
    private const int PLAYER_INDEX = 5;
    private const int TRIGGER = 1;
    private const int PLACEHOLDER = 2;
    private const int CAPTURER = 3;
    private const int SHOW_MESSAGE = 4;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Show_Message_keeps_a_400_character_text()
    {
        var box = _room.AddBox<WiredActionShowMessage>(SHOW_MESSAGE, 0, 0, "wf_act_show_message");
        var text = new string('a', 400);

        (
            await _room.SaveAsync<UpdateActionMessage>(
                SHOW_MESSAGE,
                intParams: [1, 34, -1],
                stringParam: text + "b",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        box.GetSnapshot().StringParam.Should().Be(text);
    }

    [Fact]
    public async Task The_editor_lists_the_placeholders_of_its_stack()
    {
        await BuildStackAsync();

        var snapshot = await Wired.GetBoxSnapshotAsync((RoomObjectId)SHOW_MESSAGE, Ct);
        var context = snapshot!.ContextSnapshots.OfType<WiredTextPlaceholdersSnapshot>().Single();

        context
            .ContextType.Should()
            .Be(Turbo.Primitives.Rooms.Enums.Wired.WiredContextType.TextPlaceholders);
        context.PlaceholderNames.Should().Equal("trigger_text", "who");
        context.CapturerNames.Should().Equal("p");
    }

    [Fact]
    public async Task Trigger_text_is_what_was_said()
    {
        await BuildStackAsync();

        await SayAsync("hello there");
        await TickAsync(2);

        _room
            .Harness.Fakes.SentToRoom<ChatMessageComposer>()
            .Select(x => x.Text)
            .Should()
            .Contain("hello there - ", "the harness avatar has no name for $(who)");
    }

    [Fact]
    public async Task Bot_Talk_keeps_its_hide_bot_name_switch()
    {
        var box = _room.AddBox<WiredActionBotTalk>(SHOW_MESSAGE, 0, 0, "wf_act_bot_talk");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                SHOW_MESSAGE,
                intParams: [1, 2, 1],
                stringParam: "Frank\thi",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(1, 2, 1);
    }

    [Fact]
    public async Task Bot_Talk_To_Avatar_keeps_its_hide_bot_name_switch()
    {
        var box = _room.AddBox<WiredActionBotTalkToAvatar>(
            SHOW_MESSAGE,
            0,
            0,
            "wf_act_bot_talk_to_avatar"
        );

        (
            await _room.SaveAsync<UpdateActionMessage>(
                SHOW_MESSAGE,
                intParams: [0, -1, 1],
                stringParam: "Frank\thi",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(0, -1, 1);
    }

    [Fact]
    public void A_hidden_name_fills_the_receiver_and_width_slots_before_it()
    {
        var packet = PacketHarness.Encode(
            new ShoutMessageComposer
            {
                ObjectId = 7,
                Text = "hi",
                Gesture = default,
                StyleId = 31,
                Links = [],
                TrackingId = -1,
                HideUsername = true,
            }
        );

        packet.PopInt().Should().Be(7);
        packet.PopString().Should().Be("hi");
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(31);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(-1);
        packet.PopInt().Should().Be(-1, "no receiver");
        packet.PopInt().Should().Be(-1, "no bubble width");
        packet.PopBoolean().Should().BeTrue();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void A_shown_name_writes_no_trailing_flag()
    {
        var packet = PacketHarness.Encode(
            new ChatMessageComposer
            {
                ObjectId = 7,
                Text = "hi",
                Gesture = default,
                StyleId = 31,
                Links = [],
                TrackingId = -1,
                ChatBubbleWidthOverride = 1,
            }
        );

        packet.PopInt();
        packet.PopString();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt().Should().Be(-1);
        packet.PopInt().Should().Be(1);
        packet.Remaining.Should().Be(0);
    }

    /// <summary>
    /// "User Says Keyword" (any text), a username placeholder "who", a capturer "p" and Show
    /// Message "$(trigger_text) - $(who)" to the whole room.
    /// </summary>
    private async Task BuildStackAsync()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddBox<WiredTriggerHabboSaysKeyword>(TRIGGER, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredAddonUsernamePlaceholder>(
            PLACEHOLDER,
            0,
            0,
            "wf_xtra_text_output_username"
        );
        _room.AddBox<WiredAddonVariableCapturer>(CAPTURER, 0, 0, "wf_xtra_text_input_variable");
        _room.AddBox<WiredActionShowMessage>(SHOW_MESSAGE, 0, 0, "wf_act_show_message");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                intParams: [0, 2, 0],
                stringParam: ""
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(PLACEHOLDER, intParams: [0], stringParam: "who"))
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(CAPTURER, intParams: [0], stringParam: "p"))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                SHOW_MESSAGE,
                intParams: [1, 34, -1],
                stringParam: "$(trigger_text) - $(who)",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private Task SayAsync(string text) =>
        Wired.OnRoomEventAsync(
            new PlayerChatEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
                PlayerId = (PlayerId)(100 + PLAYER_INDEX),
                ObjectId = PLAYER_INDEX,
                ChatType = RoomChatType.Chat,
                StyleId = 0,
                TrackingId = 0,
                Gesture = default,
                Text = text,
            },
            Ct
        );

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
