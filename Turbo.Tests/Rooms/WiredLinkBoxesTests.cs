using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The WIRED Links boxes in a room, as the official hotel saves, refuses and runs them (official
/// client capture 2026-10-21, build WIN63-202610201032: Hutch.'s rooms, a Link with "Receive
/// messages sent by this room" on, Show Message / Send Link Message / speakers around it).
/// </summary>
public sealed class WiredLinkBoxesTests
{
    private const int LINK = 30;
    private const int LAMP = 31;
    private const int VIEWER = 4;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredLinkBoxesTests()
    {
        _room.Harness.Fakes.Handlers["Filter"] = call => call.Args[0];
        AddLink(LINK, 7, 7);
        _room.AddFloorItem(LAMP, 6, 7);
    }

    private void AddLink(int id, int x, int y) =>
        _room.AddFloorItem(
            id,
            x,
            y,
            FurnitureWiredLinkLogic.CLASSNAME,
            canWalk: false,
            createLogic: (factory, ctx) => new FurnitureWiredLinkLogic(factory, ctx)
        );

    [Fact]
    public async Task A_message_over_1000_bytes_is_refused_as_too_much_text()
    {
        _room.AddBox<WiredActionSendMessage>(1, 0, 0, "wf_act_send_message");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [WiredActionSendMessage.SOURCE_CUSTOM],
            stringParam: new string('é', 501),
            definitionSpecifics: [0]
        );
        var saved = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [WiredActionSendMessage.SOURCE_CUSTOM],
            stringParam: new string('é', 500),
            definitionSpecifics: [0]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.too_much_text");
        refused.Parameters.Should().Equal(("limit", "1000"));
        saved.IsSaved.Should().BeTrue();
    }

    [Theory]
    [InlineData(WiredActionSendMessage.SOURCE_TRIGGER, "", true)]
    [InlineData(WiredActionSendMessage.SOURCE_CUSTOM, "relay: $(trigger_text)", true)]
    [InlineData(WiredActionSendMessage.SOURCE_CUSTOM, "plain", false)]
    public async Task Sending_the_triggers_text_gets_the_room_its_entry_notice(
        int source,
        string text,
        bool notice
    )
    {
        _room.AddBox<WiredActionSendMessage>(1, 0, 0, "wf_act_send_message");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [source],
                stringParam: text,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        (await Wired.GetTriggerTextNoticeAsync(Ct) is not null).Should().Be(notice);
    }

    [Fact]
    public async Task A_message_source_other_than_trigger_or_custom_is_refused_as_an_invalid_dataframe()
    {
        _room.AddBox<WiredActionSendMessage>(1, 0, 0, "wf_act_send_message");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [2],
            definitionSpecifics: [0]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.link.invalid_dataframe");
    }

    [Fact]
    public async Task Send_and_receive_boxes_pick_only_links_but_may_save_with_none()
    {
        _room.AddBox<WiredActionSendMessage>(1, 0, 0, "wf_act_send_message");
        _room.AddBox<WiredTriggerReceiveMessage>(2, 0, 1, "wf_trg_receive_message");

        var lamp = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [0],
            stuffIds: [LAMP],
            definitionSpecifics: [0]
        );

        lamp.ErrorKey.Should().Be("wiredfurni.error.require_link_furni");
        (await _room.SaveAsync<UpdateActionMessage>(1, intParams: [0], definitionSpecifics: [0]))
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateTriggerMessage>(2)).Should().BeTrue();
        (await _room.SaveAsync<UpdateTriggerMessage>(2, stuffIds: [LINK])).Should().BeTrue();
    }

    [Fact]
    public async Task A_send_dataframe_short_of_its_last_row_is_padded_with_a_context_target()
    {
        var box = _room.AddBox<WiredActionSendDataframe>(1, 0, 0, "wf_act_send_dataframe");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [2, 0, 42, -20, 3, 0, -20, .. Enumerable.Repeat(0, 27)],
                stringParam: """[["score",""],["greeting","hi_there"]]""",
                definitionSpecifics: [0],
                variableIds: [.. Enumerable.Repeat("0", 12)]
            )
        )
            .Should()
            .BeTrue();

        var ints = box.GetSnapshot().IntParams;

        ints.Should().HaveCount(37);
        ints[^1].Should().Be(-20);
        box.GetDefaultIntParams()
            .Should()
            .Equal([0, .. Enumerable.Range(0, 12).SelectMany(_ => new[] { 0, 0, -20 })]);
    }

    [Theory]
    [InlineData(13, """[]""", "wiredfurni.error.link.invalid_dataframe")]
    [InlineData(1, """[["",""]]""", "wiredfurni.error.link.key_length")]
    [InlineData(
        1,
        """[["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",""]]""",
        "wiredfurni.error.link.key_length"
    )]
    [InlineData(1, """[["Bad-Key",""]]""", "wiredfurni.error.variables.name_syntax")]
    [InlineData(2, """[["a",""],["a",""]]""", "wiredfurni.error.variables.name_uniq")]
    [InlineData(1, "not json", "wiredfurni.error.link.invalid_dataframe")]
    [InlineData(2, """[["a",""]]""", "wiredfurni.error.link.invalid_dataframe")]
    public async Task A_dataframe_the_editor_would_refuse_is_refused_with_its_text(
        int rows,
        string text,
        string errorKey
    )
    {
        _room.AddBox<WiredActionSendDataframe>(1, 0, 0, "wf_act_send_dataframe");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [rows, .. Enumerable.Repeat(0, 36)],
            stringParam: text,
            definitionSpecifics: [0],
            variableIds: [.. Enumerable.Repeat("0", 12)]
        );

        refused.ErrorKey.Should().Be(errorKey);
    }

    [Fact]
    public async Task A_custom_message_row_over_200_bytes_is_too_much_text()
    {
        _room.AddBox<WiredActionSendDataframe>(1, 0, 0, "wf_act_send_dataframe");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            1,
            intParams: [1, 3, 0, -20, .. Enumerable.Repeat(0, 33)],
            stringParam: $$"""[["note","{{new string('x', 201)}}"]]""",
            definitionSpecifics: [0],
            variableIds: [.. Enumerable.Repeat("0", 12)]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.too_much_text");
        refused.Parameters.Should().Equal(("limit", "200"));
    }

    [Fact]
    public async Task A_receive_dataframe_may_not_capture_into_trigger_text()
    {
        _room.AddBox<WiredTriggerReceiveDataframe>(1, 0, 0, "wf_trg_receive_dataframe");

        var refused = await _room.SaveWithResultAsync<UpdateTriggerMessage>(
            1,
            intParams: [1, .. Enumerable.Repeat(0, 12)],
            stringParam: """[["msg","trigger_text"]]""",
            stuffIds: [LINK],
            variableIds: [.. Enumerable.Repeat("0", 12)]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.placeholder.reserved");
    }

    [Fact]
    public async Task A_send_goes_to_the_owner_of_its_links_and_flips_the_link()
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionSendMessage>(2, 0, 0, "wf_act_send_message");
        await SaveClickAsync(1);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [WiredActionSendMessage.SOURCE_CUSTOM],
                stringParam: "lab hello",
                stuffIds: [LINK],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 0);

        var before = _room.FloorItem(LINK).Logic.GetState();

        await ClickLampAsync();

        var send = _room
            .Harness.Fakes.Log.Of(nameof(IWiredLinkOwnerGrain.SendAsync))
            .Should()
            .ContainSingle()
            .Subject;

        ((ImmutableArray<int>)send.Args[1]!).Should().Equal(LINK);
        ((WiredLinkPayload)send.Args[2]!).Message.Should().Be("lab hello");
        _room.FloorItem(LINK).Logic.GetState().Should().NotBe(before);
    }

    [Fact]
    public async Task From_wired_trigger_with_no_text_sends_null()
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionSendMessage>(2, 0, 0, "wf_act_send_message");
        await SaveClickAsync(1);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [WiredActionSendMessage.SOURCE_TRIGGER],
                stuffIds: [LINK],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 0);

        await ClickLampAsync();

        var send = _room.Harness.Fakes.Log.Of(nameof(IWiredLinkOwnerGrain.SendAsync)).Single();

        ((WiredLinkPayload)send.Args[2]!).Message.Should().Be("null");
    }

    [Fact]
    public async Task A_received_message_is_the_stacks_trigger_text()
    {
        _room.AddBox<WiredTriggerReceiveMessage>(1, 0, 0, "wf_trg_receive_message");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");
        (await _room.SaveAsync<UpdateTriggerMessage>(1, stuffIds: [LINK])).Should().BeTrue();
        await LogAsync(2, "relay: $(trigger_text)");
        await StartAsync(0, 0);

        await ReceiveAsync(new WiredLinkPayload { SourceRoomId = 2, Message = "lab hello" });

        Wired.GetRoomLogs().Select(x => x.Message).Should().Contain("relay: lab hello");
    }

    [Fact]
    public async Task A_receive_trigger_ignores_links_it_did_not_pick_and_the_other_kind()
    {
        AddLink(LINK + 10, 5, 7);
        _room.AddBox<WiredTriggerReceiveMessage>(1, 0, 0, "wf_trg_receive_message");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");
        (await _room.SaveAsync<UpdateTriggerMessage>(1, stuffIds: [LINK])).Should().BeTrue();
        await LogAsync(2, "got $(trigger_text)");
        await StartAsync(0, 0);

        await ReceiveAsync(new WiredLinkPayload { SourceRoomId = 2, Message = "x" }, LINK + 10);
        await ReceiveAsync(new WiredLinkPayload { SourceRoomId = 2, Rows = [new("a", "1")] });

        Wired.GetRoomLogs().Should().BeEmpty();
    }

    [Fact]
    public async Task A_received_dataframe_fills_its_placeholders_and_not_trigger_text()
    {
        _room.AddBox<WiredTriggerReceiveDataframe>(1, 0, 0, "wf_trg_receive_dataframe");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                intParams: [2, .. Enumerable.Repeat(0, 12)],
                stringParam: """[["score","score"],["greeting","greet"]]""",
                stuffIds: [LINK],
                variableIds: [.. Enumerable.Repeat("0", 12)]
            )
        )
            .Should()
            .BeTrue();
        await LogAsync(2, "df: $(score) / $(greet)");
        await StartAsync(0, 0);

        await ReceiveAsync(
            new WiredLinkPayload
            {
                SourceRoomId = 2,
                Rows = [new("score", "42"), new("greeting", "hi_there")],
            }
        );

        Wired.GetRoomLogs().Select(x => x.Message).Should().Contain("df: 42 / hi_there");
    }

    [Fact]
    public async Task A_speaker_alone_on_its_tile_says_a_message_at_its_own_tile_to_each_viewer()
    {
        _room.Enter(VIEWER, 1, 1);
        _room.AddBox<WiredAddonLinkSpeaker>(1, 3, 2, "wf_xtra_link_speaker");
        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [1, 1], stuffIds: [LINK]))
            .Should()
            .BeTrue();
        await StartAsync(3, 2);

        await ReceiveAsync(new WiredLinkPayload { SourceRoomId = 2, Message = "lab hello" });

        var bubble = BubblesTo(100 + VIEWER).Should().ContainSingle().Subject;

        bubble.Text.Should().Be("lab hello");
        bubble.StyleId.Should().Be(253);
        bubble.ObjectId.Value.Should().Be(-1);
        bubble.TrackingId.Should().Be(-1);
        bubble.ReceiverRoomIndex.Should().Be(VIEWER);
        bubble.HideUsername.Should().BeTrue();
        bubble.Location.Should().Be(new ChatBubbleLocation(3, 2));
    }

    [Fact]
    public async Task A_speaker_says_a_dataframe_row_by_row_with_bold_keys_unless_it_speaks_for_messages_only()
    {
        _room.Enter(VIEWER, 1, 1);
        _room.AddBox<WiredAddonLinkSpeaker>(1, 3, 2, "wf_xtra_link_speaker");
        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [1, 1], stuffIds: [LINK]))
            .Should()
            .BeTrue();
        await StartAsync(3, 2);

        var dataframe = new WiredLinkPayload
        {
            SourceRoomId = 2,
            Rows = [new("score", "42"), new("greeting", "hi_there")],
        };

        await ReceiveAsync(dataframe);

        BubblesTo(100 + VIEWER)
            .Single()
            .Text.Should()
            .Be("[b]score[/b]: 42\n[b]greeting[/b]: hi_there");

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [1, 0], stuffIds: [LINK]))
            .Should()
            .BeTrue();
        await StartAsync(3, 2);

        await ReceiveAsync(dataframe);

        BubblesTo(100 + VIEWER).Should().HaveCount(1);
    }

    private IEnumerable<ChatMessageComposer> BubblesTo(int playerId) =>
        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(playerId);

    private async Task ReceiveAsync(WiredLinkPayload payload, int link = LINK)
    {
        await _room.Harness.Room.ReceiveWiredLinkPayloadAsync([link], payload, Ct);
        await TickAsync(2);
    }

    private async Task SaveClickAsync(int boxId) => (
            await _room.SaveAsync<UpdateTriggerMessage>(
                boxId,
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

    private async Task LogAsync(int boxId, string text) =>
        (
            await _room.SaveAsync<UpdateActionMessage>(
                boxId,
                intParams: [(int)WiredLogLevelType.Info],
                stringParam: text,
                definitionSpecifics: [2]
            )
        )
            .Should()
            .BeTrue();

    private async Task ClickLampAsync()
    {
        _room.Enter(VIEWER, 1, 1);

        await _room
            .FloorItem(LAMP)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + VIEWER), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(2);
    }

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
