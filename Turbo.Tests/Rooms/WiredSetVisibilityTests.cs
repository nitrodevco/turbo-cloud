using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Effect: Set Visibility (<c>ActionTypeCodes.SET_FURNI_TRANSPARENCY</c> 61,
/// <c>actiontypes/_-z1S</c>, WIN63-202610201032). Int params [audience everyone, transparency
/// 0/100, allow click through, easing 0-4, target is users]; furni slot 0 the target furni,
/// user slot 0 the audience, user slot 1 the target users. A player clicks a furni to run it.
/// </summary>
public sealed class WiredSetVisibilityTests
{
    private const int CLICKER = 5;
    private const int OTHER = 6;
    private const int CLICK_ME = 23;
    private const int TARGET = 24;
    private const int WALL_TARGET = 40;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    /// <summary>Official client capture 2026-10-20: a fresh box opens with [0, 100, 0, 4, 0].</summary>
    [Fact]
    public async Task A_fresh_box_opens_invisible_with_ease_in_out()
    {
        var box = _room.AddBox<WiredActionSetFurniTransparency>(2, 0, 0, "wf_act_set_visibility");

        await box.LoadWiredAsync(Ct);

        box.GetSnapshot().IntParams.Should().Equal(0, 100, 0, 4, 0);
    }

    [Theory]
    [InlineData(new[] { 0, 0, 0, 0, 0 }, true)]
    [InlineData(new[] { 1, 100, 1, 4, 1 }, true)]
    [InlineData(new[] { 0, 100, 0, 5, 0 }, false)]
    [InlineData(new[] { 2, 100, 0, 0, 0 }, false)]
    public async Task Saves_what_its_editor_writes(int[] intParams, bool saved)
    {
        _room.AddBox<WiredActionSetFurniTransparency>(2, 0, 0, "wf_act_set_visibility");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: intParams,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .Be(saved);
    }

    /// <summary>Official client capture 2026-10-20: 50 -> the raw key (the texts have no entry).</summary>
    [Fact]
    public async Task A_half_transparency_is_refused_with_the_hotels_text()
    {
        _room.AddBox<WiredActionSetFurniTransparency>(2, 0, 0, "wf_act_set_visibility");

        var result = await _room.SaveWithResultAsync<UpdateActionMessage>(
            2,
            intParams: [0, 50, 0, 0, 0],
            definitionSpecifics: [0]
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be("wiredfurni.error.set_furni_transparency.invalid_transparency");
    }

    [Fact]
    public async Task Selected_users_alone_see_the_furni_fade()
    {
        await SetUpAsync(intParams: [0, 100, 1, 2, 0], stuffIds: [TARGET]);

        await ClickAsync();

        var sent = _room
            .Harness.Fakes.SentToPlayer<WiredFurniTransparencyMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Subject;

        sent.FurniIds.Should().Equal(TARGET);
        sent.Transparency.Should().Be(100);
        sent.AllowClickThrough.Should().BeTrue();
        sent.EasingMode.Should().Be(2);
        _room
            .Harness.Fakes.SentToPlayer<WiredFurniTransparencyMessageComposer>(100 + OTHER)
            .Should()
            .BeEmpty();
        _room.Harness.Fakes.SentToRoom<WiredFurniTransparencyMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Everyone_sees_it_through_the_room()
    {
        await SetUpAsync(intParams: [1, 100, 0, 0, 0], stuffIds: [TARGET]);

        await ClickAsync();

        _room
            .Harness.Fakes.SentToRoom<WiredFurniTransparencyMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.FurniIds.Should()
            .Equal(TARGET);
        _room
            .Harness.Fakes.SentToPlayer<WiredFurniTransparencyMessageComposer>(100 + CLICKER)
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// "Everyone, including users who enter later": a player walking in is told in their entry,
    /// drawn without the fade; one shown to selected users alone is not kept.
    /// </summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task Only_an_everyone_effect_is_kept_for_players_who_enter_later(
        int audience,
        bool kept
    )
    {
        await SetUpAsync(intParams: [audience, 100, 1, 3, 0], stuffIds: [TARGET]);

        await ClickAsync();

        var state = (await EntryStateAsync()).OfType<WiredFurniVisualizationStateMessageComposer>();

        if (!kept)
        {
            state.Should().BeEmpty();

            return;
        }

        state
            .Should()
            .ContainSingle()
            .Which.Transparencies.Select(Entry)
            .Should()
            .Equal((TARGET, 100, true, 3));
    }

    /// <summary>
    /// "Stack this WIRED Effect with WIRED Trigger: Room Initialized" (the everyone alert): fired
    /// on the room's first tick, with nobody to trigger it, an everyone effect is kept for the
    /// first player who walks in.
    /// </summary>
    [Fact]
    public async Task Room_initialized_keeps_an_everyone_effect_for_the_first_player_in()
    {
        _room.AddFloorItem(TARGET, 6, 6);
        _room.AddBox<WiredTriggerRoomInitialized>(1, 3, 0, "wf_trg_room_initializes");
        _room.AddBox<WiredActionSetFurniTransparency>(2, 3, 0, "wf_act_set_visibility");

        (await _room.SaveAsync<UpdateTriggerMessage>(1)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [1, 100, 0, 0, 0],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(3, 0)],
            },
            Ct
        );
        await TickAsync(1);

        (await EntryStateAsync())
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Transparencies.Select(Entry)
            .Should()
            .Equal((TARGET, 100, false, 0));
    }

    [Fact]
    public async Task Users_made_invisible_for_everyone_are_kept_by_room_index()
    {
        await SetUpAsync(intParams: [1, 100, 0, 0, 1]);

        await ClickAsync();

        (await EntryStateAsync())
            .OfType<WiredUserVisualizationStateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Transparencies.Select(Entry)
            .Should()
            .Equal((CLICKER, 100, false, 0));
    }

    /// <summary>Official client capture 2026-10-20: a target picked up drops its entry.</summary>
    [Fact]
    public async Task A_furni_taken_away_is_no_longer_kept()
    {
        _room.AddWallItem(WALL_TARGET);

        await SetUpAsync(intParams: [1, 100, 0, 0, 0], stuffIds: [TARGET, -WALL_TARGET]);
        await ClickAsync();

        (await EntryStateAsync())
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Single()
            .Transparencies.Select(x => x.Id)
            .Should()
            .BeEquivalentTo([TARGET, -WALL_TARGET]);

        await Wired.OnRoomEventAsync(
            new RoomItemDetachedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                ObjectId = WALL_TARGET,
            },
            Ct
        );

        (await EntryStateAsync())
            .OfType<WiredFurniVisualizationStateMessageComposer>()
            .Single()
            .Transparencies.Select(x => x.Id)
            .Should()
            .Equal(TARGET);
    }

    /// <summary>Official client capture 2026-10-20: nothing kept, no state message (3 of 3 entries).</summary>
    [Fact]
    public async Task An_entry_gets_no_state_message_when_nothing_is_kept()
    {
        await SetUpAsync(intParams: [0, 100, 0, 0, 0], stuffIds: [TARGET]);

        await ClickAsync();

        (await EntryStateAsync())
            .Where(x =>
                x
                    is WiredFurniVisualizationStateMessageComposer
                        or WiredUserVisualizationStateMessageComposer
            )
            .Should()
            .BeEmpty();
    }

    /// <summary>The client reads a negative id as a wall item (RoomEngine.getWiredFurniCategory).</summary>
    [Fact]
    public async Task A_wall_item_goes_by_its_negative_id()
    {
        _room.AddWallItem(WALL_TARGET);

        await SetUpAsync(intParams: [0, 100, 0, 0, 0], stuffIds: [-WALL_TARGET, TARGET]);

        await ClickAsync();

        _room
            .Harness.Fakes.SentToPlayer<WiredFurniTransparencyMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Which.FurniIds.Should()
            .BeEquivalentTo([-WALL_TARGET, TARGET]);
    }

    [Fact]
    public async Task Target_users_fade_by_their_room_index()
    {
        await SetUpAsync(intParams: [0, 100, 0, 1, 1]);

        await ClickAsync();

        var sent = _room
            .Harness.Fakes.SentToPlayer<WiredUserTransparencyMessageComposer>(100 + CLICKER)
            .Should()
            .ContainSingle()
            .Subject;

        sent.RoomIndices.Should().Equal(CLICKER);
        sent.EasingMode.Should().Be(1);
        _room
            .Harness.Fakes.SentToPlayer<WiredFurniTransparencyMessageComposer>(100 + CLICKER)
            .Should()
            .BeEmpty();
    }

    private async Task SetUpAsync(int[] intParams, int[]? stuffIds = null)
    {
        _room.Enter(CLICKER, 1, 1);
        _room.Enter(OTHER, 2, 2);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(TARGET, 6, 6);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionSetFurniTransparency>(2, 0, 0, "wf_act_set_visibility");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: intParams,
                stuffIds: stuffIds,
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

        await RebuildAsync();
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(4);
    }

    /// <summary>What a player walking in is told of the room's wired (the entry tail).</summary>
    // The kept state rides in the room entry view (right after the first Users), not the wired entry tail.
    private Task<ImmutableArray<IComposer>> EntryStateAsync() =>
        Task.FromResult(Wired.GetVisualizationEntryState().ToImmutableArray());

    private static (int Id, int Transparency, bool ClickThrough, int Easing) Entry(
        WiredTransparencyStateEntry x
    ) =>
        (
            x.Id,
            x.Transparency.Transparency,
            x.Transparency.AllowClickThrough,
            x.Transparency.EasingMode
        );

    private async Task RebuildAsync()
    {
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

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
