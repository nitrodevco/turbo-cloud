using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Context;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>@selector_user_count</c> and <c>@selector_furni_count</c> count what the running stack's
/// selectors picked. The Wired Faculty tutorial "Ticket queue system" (07/06/2025) shows a
/// player's place in the queue with <c>$(selector_user_count)</c>, and "Stacking unstackable
/// furni" (15/03/2025) multiplies by <c>@selector_furni_count</c>. The furni count always read 0
/// and the user count did not exist.
/// </summary>
public sealed class WiredSelectorCountTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task The_users_the_selector_picked_are_counted()
    {
        _room.Enter(PLAYER_INDEX, 3, 3);
        _room.Enter(PLAYER_INDEX + 1, 4, 4);
        _room.Enter(PLAYER_INDEX + 2, 3, 4);
        _room.Enter(PLAYER_INDEX + 3, 7, 7);
        var count = Register(new ContextSelectorUserCountVariable(_room.Harness.Room));

        _room.AddBox<WiredSelectorEntitiesInArea>(2, 0, 0, "wf_slc_users_area");
        (await _room.SaveAsync<UpdateSelectorMessage>(2, intParams: [3, 3, 2, 2]))
            .Should()
            .BeTrue();

        (await RunAsync(count)).Should().Be(3);
    }

    [Fact]
    public async Task The_furni_the_selector_picked_are_counted()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(20, 1, 2, definitionId: 77);
        _room.AddFloorItem(21, 2, 2, definitionId: 77);
        _room.AddFloorItem(22, 6, 6, definitionId: 78);
        var count = Register(new ContextSelectorFurniCountVariable(_room.Harness.Room));

        _room.AddBox<WiredSelectorItemsByType>(2, 0, 0, "wf_slc_furni_bytype");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                2,
                intParams: [0],
                stuffIds: [20],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        (await RunAsync(count)).Should().Be(2);
    }

    /// <summary>
    /// "queue" said fires the stack on tile (0, 0), whose Change Variable sets the global
    /// "place" to the count; the count variable is read as the action runs.
    /// </summary>
    private async Task<long> RunAsync(WiredVariableId count)
    {
        var place = _room.AddBox<WiredVariableRoom>(10, 5, 5, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "place"
            )
        )
            .Should()
            .BeTrue();
        await place.LoadWiredAsync(Ct);
        await StartAsync(5, 5);

        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");
        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 0, 0], stringParam: "queue"))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [0],
                variableIds: [place.GetVarSnapshot().VariableId.ToString(), count.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 0);

        await Wired.OnRoomEventAsync(
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
                Text = "queue",
            },
            Ct
        );
        await TickAsync(4);

        place
            .TryGetValue(
                new WiredVariableKey(
                    place.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Global,
                    0
                ),
                out var value
            )
            .Should()
            .BeTrue();

        return value.Value;
    }

    private WiredVariableId Register(WiredInternalVariable variable)
    {
        var id = variable.GetVarSnapshot().VariableId;

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[id] =
            variable;

        return id;
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
