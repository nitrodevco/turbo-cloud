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
        var sent = _room
            .Harness.Fakes.SentToRoom<WiredFurniHighlightMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        sent.FurniIds.Should().Equal(LAMP);
        sent.Highlight.Should().Be(expected);

        var state = EntryView()
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
    public void A_room_without_a_highlight_tells_a_newcomer_nothing_of_it() =>
        EntryView().Should().BeEmpty();

    [Fact]
    public async Task The_kept_state_rides_in_the_entry_view_not_the_wired_entry_tail()
    {
        // Official client capture (lab60, 3/3): right after the first Users, before Objects.
        Wired.KeepFurniHighlight([LAMP], new WiredHighlightSnapshot { Type = 0, Strength = 50 });

        (await _room.Harness.Room.GetEntryViewAsync((PlayerId)999, Ct))
            .WiredVisualization.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<WiredFurniVisualizationStateMessageComposer>();
        Wired
            .GetEntryState()
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Shown_to_the_selected_users_it_goes_to_them_alone_and_is_not_kept()
    {
        _room.AddFloorItem(LAMP, 5, 5);
        await ClickStackAsync(everyone: false, type: 0, strength: 100);

        await ClickAsync();

        _room
            .Harness.Fakes.SentToPlayer<WiredFurniHighlightMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Which.FurniIds.Should()
            .Equal(LAMP);
        _room
            .Harness.Fakes.SentToPlayer<WiredFurniHighlightMessageComposer>(100 + CLICKED)
            .Should()
            .BeEmpty();
        _room.Harness.Fakes.SentToRoom<WiredFurniHighlightMessageComposer>().Should().BeEmpty();
        EntryView().OfType<WiredFurniVisualizationStateMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Users_as_targets_are_named_by_room_index_and_forgotten_when_they_leave()
    {
        await ClickStackAsync(everyone: true, type: 4, strength: 38, targetUsers: true);

        await ClickAsync();

        _room
            .Harness.Fakes.SentToRoom<WiredUserHighlightMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.RoomIndices.Should()
            .Equal(CLICKER);
        EntryView()
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

        EntryView().OfType<WiredUserVisualizationStateMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Strength_off_stays_kept_and_a_furni_leaving_the_room_drops_it()
    {
        // Official client capture (lab60): a strength-0 fire stays in the entry state as an entry;
        // picking up the target drops its entry.
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

        EntryView()
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Single()
            .Highlights.Should()
            .Equal(
                new WiredHighlightStateEntry
                {
                    Id = 21,
                    Highlight = new WiredHighlightSnapshot { Type = 0, Strength = 0 },
                }
            );
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(50, false)]
    public async Task Darken_saves_only_the_hotel_darken_levels(int strength, bool saved)
    {
        _room.AddFloorItem(LAMP, 5, 5);
        _room.AddBox<WiredActionSetHighlight>(EFFECT, 0, 0, "wf_act_set_highlight");

        var result = await _room.SaveWithResultAsync<UpdateActionMessage>(
            EFFECT,
            intParams: [1, 2, strength, 0, 0, 2, 0],
            stuffIds: [LAMP],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            definitionSpecifics: [0]
        );

        result.IsSaved.Should().Be(saved);

        if (!saved)
            result.ErrorKey.Should().Be(WiredSaveErrors.SET_FURNI_HIGHLIGHT_INVALID_STRENGTH);
    }

    [Fact]
    public void A_new_box_opens_with_the_official_defaults()
    {
        var box = _room.AddBox<WiredActionSetHighlight>(EFFECT, 0, 0, "wf_act_set_highlight");

        box.GetDefaultIntParams().Should().Equal(0, 0, 100, 16771584, 0, 4, 0);
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

    /// <summary>What a player walking in is sent of the kept effects, right after Users (RoomService).</summary>
    private List<IComposer> EntryView() => [.. Wired.GetVisualizationEntryState()];
}
