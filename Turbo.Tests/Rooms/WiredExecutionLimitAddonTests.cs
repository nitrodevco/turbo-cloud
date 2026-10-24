using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The execution limit add-on, set to 1 execution per 10 pulses (5 s), on a fresh stack: a click
/// teleports the clicker onto a furni. Measured on the official server (confidential capture,
/// addon.5.R4): the first activation runs, the next one inside the window is blocked, and one
/// after the window runs again.
/// </summary>
public sealed class WiredExecutionLimitAddonTests
{
    private const int CLICK_ME = 20;
    private const int TARGET = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredExecutionLimitAddonTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(TARGET, 5, 5);
        _room.Enter(5, 1, 1);
    }

    [Fact]
    public async Task Addon5R4_OneExecutionPerWindow_BlocksTheSecondClickUntilTheWindowEnds()
    {
        await BuildAsync(executions: 1, windowPulses: 10);

        await ClickAsync();
        _room.Positions().Should().Equal("5@5,5");

        await ReturnUserAsync();
        await ClickAsync();
        // The second click is inside the 5 s window.
        _room.Positions().Should().Equal("5@1,1");

        _room.Clock.AdvanceMs(5_000);
        await ClickAsync();
        _room.Positions().Should().Equal("5@5,5");
    }

    private async Task BuildAsync(int executions, int windowPulses)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredAddonExecutionLimit>(2, 0, 0, "wf_xtra_execution_limit");
        _room.AddBox<WiredActionTeleportTo>(3, 0, 0, "wf_act_teleport_to");

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
        (await _room.SaveAsync<UpdateAddonMessage>(2, intParams: [executions, windowPulses]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [1],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

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
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);
    }

    private Task ReturnUserAsync() =>
        _room
            .Harness.Module<RoomAvatarModule>()
            .RelocateAvatarAsync(_room.Avatars[5], _room.Map.ToIdx(1, 1), Ct);

    // The ticks move only the wired system's clock; the add-on reads the room's, so a click and
    // its ticks take no room time.
    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
