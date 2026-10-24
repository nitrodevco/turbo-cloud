using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// How a stack's selectors add up, as measured on an official room (official client capture): a
/// "filter existing selection" selector picks nothing by itself, not even the user who triggered
/// the stack; it narrows the other selectors' picks wherever it stands in the stack; and filtering
/// or inverting touches only the selector's own kind, furni or users. One stack: a user clicks a
/// furni, the selectors run, and "teleport to" moves the users the selectors picked onto a furni.
/// </summary>
public sealed class WiredSelectorFilterSeedTests
{
    private const int CLICK_ME = 20;
    private const int STAND = 21;
    private const int TARGET = 22;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private int _nextBox = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSelectorFilterSeedTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(STAND, 1, 3);
        _room.AddFloorItem(TARGET, 5, 5);
        _room.Enter(5, 1, 1);
        _room.Enter(6, 1, 3);
    }

    [Fact]
    public async Task AFilterAlone_PicksNobody_NotEvenTheUserWhoClicked()
    {
        await AddTriggerAsync();
        await AddUsersByTypeAsync(filter: true, invert: false);
        await AddTeleportAsync();

        await RunAsync(clickedBy: 5);

        _room.Positions().Should().Equal("5@1,1", "6@1,3");
    }

    [Fact]
    public async Task AFilterBelowItsSeed_StillNarrowsWhatTheSeedPicked()
    {
        await AddTriggerAsync();
        await AddUsersOnStandAsync(filter: true);
        await AddUsersByTypeAsync(filter: false, invert: false);
        await AddTeleportAsync();

        await RunAsync(clickedBy: 5);

        // Every player was picked, and only 6 stands on the furni the filter looks at.
        _room.Positions().Should().Equal("5@1,1", "6@5,5");
    }

    [Fact]
    public async Task AFurniFilter_LeavesTheUsersTheOtherSelectorsPicked()
    {
        await AddTriggerAsync();
        await AddUsersByTypeAsync(filter: false, invert: false);
        await AddFurniByTypeAsync(filter: true);
        await AddTeleportAsync();

        await RunAsync(clickedBy: 5);

        _room.Positions().Should().Equal("5@5,5", "6@5,5");
    }

    [Fact]
    public async Task AnInvertedUserSelector_PicksTheOtherUsersAndNoFurni()
    {
        await AddTriggerAsync();
        // No pet is in the room, so "not a pet" is every player.
        await AddUsersByTypeAsync(filter: false, invert: true, kinds: 2);
        await AddNoSelectedFurniConditionAsync();
        await AddTeleportAsync();

        await RunAsync(clickedBy: 5);

        _room.Positions().Should().Equal("5@5,5", "6@5,5");
    }

    // selector.11.R6: an inverted "users by name" picks everyone but the named user, and everyone
    // when nobody has the name (confidential capture, measured: selector inversion).
    [Theory]
    [InlineData("Ann", new[] { "5@1,1", "6@5,5" })]
    [InlineData("Zed", new[] { "5@5,5", "6@5,5" })]
    public async Task Selector11R6_AnInvertedNameSelector_PicksEveryoneButTheNamedUser(
        string name,
        string[] positions
    )
    {
        NameUsers();
        await AddTriggerAsync();
        await AddUsersByNameAsync(name, invert: true);
        await AddTeleportAsync();

        await RunAsync(clickedBy: 5);

        _room.Positions().Should().Equal(positions);
    }

    // selector.11.R5 / selector.2.R5: a "users by type" filter keeps a named Habbo and removes it
    // when filtering for pets (confidential capture, measured: a filter needs a seed selection).
    [Theory]
    [InlineData(1, new[] { "5@5,5", "6@1,3" })]
    [InlineData(2, new[] { "5@1,1", "6@1,3" })]
    public async Task Selector11R5_ATypeFilter_NarrowsTheUserTheNameSelectorPicked(
        int kinds,
        string[] positions
    )
    {
        NameUsers();
        await AddTriggerAsync();
        await AddUsersByNameAsync("Ann", invert: false);
        await AddUsersByTypeAsync(filter: true, invert: false, kinds: kinds);
        await AddTeleportAsync();

        await RunAsync(clickedBy: 6);

        _room.Positions().Should().Equal(positions);
    }

    private void NameUsers()
    {
        _room.Avatars[5].UseName("Ann");
        _room.Avatars[6].UseName("Bob");
    }

    private async Task AddUsersByNameAsync(string names, bool invert)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorEntitiesByName>(id, 0, 0, "wf_slc_users_byname");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                stringParam: names,
                definitionSpecifics: [false, invert]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task AddTriggerAsync()
    {
        var id = _nextBox++;

        _room.AddBox<WiredTriggerClickFurni>(id, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                id,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
    }

    private async Task AddUsersByTypeAsync(bool filter, bool invert, int kinds = 1)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorEntitiesByType>(id, 0, 0, "wf_slc_users_bytype");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                intParams: [kinds],
                definitionSpecifics: [filter, invert]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task AddUsersOnStandAsync(bool filter)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorEntitiesOnItem>(id, 0, 0, "wf_slc_users_onfurni");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                stuffIds: [STAND],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [filter, false]
            )
        ).Should().BeTrue();
    }

    private async Task AddFurniByTypeAsync(bool filter)
    {
        var id = _nextBox++;

        _room.AddBox<WiredSelectorItemsByType>(id, 0, 0, "wf_slc_furni_bytype");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                id,
                intParams: [0],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [filter, false]
            )
        ).Should().BeTrue();
    }

    /// <summary>Input Source Quantity: the selector picked exactly 0 furni.</summary>
    private async Task AddNoSelectedFurniConditionAsync()
    {
        var id = _nextBox++;

        _room.AddBox<WiredConditionSelectQuantity>(id, 0, 0, "wf_cnd_slc_quantity");
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                id,
                intParams: [0, 0, 1],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();
    }

    private async Task AddTeleportAsync()
    {
        var id = _nextBox++;

        _room.AddBox<WiredActionTeleportTo>(id, 0, 0, "wf_act_teleport_to");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                id,
                intParams: [0],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
    }

    /// <summary>The stack forms, a user clicks the furni, and a few wired ticks run it through.</summary>
    private async Task RunAsync(int clickedBy)
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

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + clickedBy), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(6);
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
