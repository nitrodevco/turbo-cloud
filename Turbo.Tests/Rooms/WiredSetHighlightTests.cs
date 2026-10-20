using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Effect: Set Highlight (<c>wf_act_set_highlight</c>, action code 62, released 2026-10-20;
/// AS3 WIN63-202610201032 <c>actiontypes/_-d22</c>, <c>WiredFurniHighlightMessageParser</c>,
/// <c>WiredFurniVisualizationStateMessageParser</c>). It highlights furni or users for the selected
/// users or for "everyone, including users who enter later"; the latter is kept for whoever walks
/// in, and with Room Initialized it is there from the room's first tick ("To keep this effect
/// visible at all times, including after a room reload, stack this WIRED Effect with WIRED
/// Trigger: Room Initialized"). Ours had no such box.
/// </summary>
public sealed class WiredSetHighlightTests
{
    private const int TRIGGER = 1;
    private const int EFFECT = 2;
    private const int LAMP = 20;
    private const int CLICKER = 5;
    private const int CLICKED = 6;
    private const int TEAL = 2146303;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public void The_box_is_action_code_62()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_act_set_highlight");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_act_set_highlight",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredActionSetHighlight>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(62);
    }

    [Fact]
    public async Task Shown_to_everyone_from_room_initialized_it_is_sent_to_the_room_and_to_whoever_walks_in()
    {
        _room.AddFloorItem(LAMP, 5, 5);
        await InitializedStackAsync(everyone: true, type: 1, strength: 80, animation: 1);

        await TickAsync(1);

        var expected = new WiredHighlightSnapshot
        {
            Type = 1,
            Strength = 80,
            Color = TEAL,
            AnimationMode = 1,
            EasingMode = 0,
        };
        var sent = SentToRoom<WiredFurniHighlightMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        sent.FurniIds.Should().Equal(LAMP);
        sent.Highlight.Should().Be(expected);

        var state = Wired
            .GetEntryState()
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        state.Transparencies.Should().BeEmpty();
        state
            .Highlights.Should()
            .Equal(new WiredHighlightStateEntry { Id = LAMP, Highlight = expected });

        // The bytes the client's parser reads: no transparencies, one highlight.
        var packet = PacketHarness.Encode(state);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(LAMP);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(80);
        packet.PopInt().Should().Be(TEAL);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(0);
    }

    [Fact]
    public void A_room_without_a_highlight_tells_a_newcomer_nothing_of_it()
    {
        Wired
            .GetEntryState()
            .Should()
            .NotContain(x =>
                x is WiredFurniVisualizationStateMessageComposer
                || x is WiredUserVisualizationStateMessageComposer
            );
    }

    [Fact]
    public async Task Shown_to_the_selected_users_it_goes_to_them_alone_and_is_not_kept()
    {
        _room.AddFloorItem(LAMP, 5, 5);
        await ClickStackAsync(everyone: false, type: 0, strength: 100);

        await ClickAsync();

        SentTo<WiredFurniHighlightMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Which.FurniIds.Should()
            .Equal(LAMP);
        SentTo<WiredFurniHighlightMessageComposer>(100 + CLICKED).Should().BeEmpty();
        SentToRoom<WiredFurniHighlightMessageComposer>().Should().BeEmpty();
        Wired
            .GetEntryState()
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Users_as_targets_are_named_by_room_index_and_forgotten_when_they_leave()
    {
        await ClickStackAsync(everyone: true, type: 4, strength: 38, targetUsers: true);

        await ClickAsync();

        SentToRoom<WiredUserHighlightMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.RoomIndices.Should()
            .Equal(CLICKER);
        Wired
            .GetEntryState()
            .OfType<WiredUserVisualizationStateMessageComposer>()
            .Single()
            .Highlights.Select(x => x.Id)
            .Should()
            .Equal(CLICKER);

        await Wired.OnRoomEventAsync(
            new PlayerLeftEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                PlayerId = 100 + CLICKER,
                ObjectId = CLICKER,
            },
            Ct
        );

        Wired
            .GetEntryState()
            .OfType<WiredUserVisualizationStateMessageComposer>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Strength_off_and_a_furni_leaving_the_room_drop_what_was_kept()
    {
        _room.AddFloorItem(LAMP, 5, 5);
        Wired.KeepFurniHighlight(
            [LAMP, 21],
            new WiredHighlightSnapshot { Type = 0, Strength = 50 }
        );

        Wired.KeepFurniHighlight([21], new WiredHighlightSnapshot { Type = 0, Strength = 0 });
        await Wired.OnRoomEventAsync(
            new RoomItemDetachedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                ObjectId = LAMP,
            },
            Ct
        );

        Wired
            .GetEntryState()
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Should()
            .BeEmpty();
    }

    [Theory]
    [InlineData(LAMP, 0, false)]
    [InlineData(LAMP, 1, true)]
    [InlineData(FurniIdBands.BuildersClubMin + 1, 0, true)]
    public async Task A_steady_color_tint_is_refused_on_normal_furni(
        int furniId,
        int animation,
        bool saved
    )
    {
        _room.AddFloorItem(furniId, 5, 5);
        _room.AddBox<WiredActionSetHighlight>(EFFECT, 0, 0, "wf_act_set_highlight");

        var result = await _room.SaveWithResultAsync<UpdateActionMessage>(
            EFFECT,
            intParams: [1, 3, 75, TEAL, animation, 0, 0],
            stuffIds: [furniId],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            definitionSpecifics: [0]
        );

        result.IsSaved.Should().Be(saved);

        if (!saved)
            result.ErrorKey.Should().Be(WiredSaveErrors.SET_FURNI_HIGHLIGHT_NORMAL_FURNI);
    }

    private async Task InitializedStackAsync(
        bool everyone,
        int type,
        int strength,
        int animation,
        bool targetUsers = false
    )
    {
        _room.AddBox<WiredTriggerRoomInitialized>(TRIGGER, 0, 0, "wf_trg_room_initializes");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER)).Should().BeTrue();
        await SaveHighlightAsync(everyone, type, strength, animation, targetUsers);
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
    }

    private async Task SaveHighlightAsync(
        bool everyone,
        int type,
        int strength,
        int animation,
        bool targetUsers = false
    )
    {
        _room.AddBox<WiredActionSetHighlight>(EFFECT, 0, 0, "wf_act_set_highlight");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams:
                [
                    everyone ? 1 : 0,
                    type,
                    strength,
                    TEAL,
                    animation,
                    0,
                    targetUsers ? 1 : 0,
                ],
                stuffIds: targetUsers ? [] : [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
    }

    /// <summary>"User clicks User" and the highlight: the clicker is the triggering user.</summary>
    private async Task ClickStackAsync(
        bool everyone,
        int type,
        int strength,
        bool targetUsers = false
    )
    {
        _room.Enter(CLICKER, 1, 1);
        _room.Enter(CLICKED, 3, 3);
        _room.AddBox<WiredTriggerClickUser>(TRIGGER, 0, 0, "wf_trg_click_user");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [0, 0])).Should().BeTrue();
        await SaveHighlightAsync(everyone, type, strength, animation: 0, targetUsers);
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

    private async Task ClickAsync()
    {
        await _room.Harness.Room.WiredClickAvatarAsync(
            ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
            CLICKED,
            Ct
        );
        await TickAsync(2);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

    private IEnumerable<T> SentTo<T>(long playerId) =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Key is long key && key == playerId
            )
            .SelectMany(x =>
                x.Args.OfType<IComposer>()
                    .Concat(x.Args.OfType<IEnumerable<IComposer>>().SelectMany(c => c))
            )
            .OfType<T>();
}
