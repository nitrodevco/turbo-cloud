using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// sirjonasxx, variables-info #20 (Sulake): "placeholders stay active for the entire signal chain
/// (unless they are overwritten). Example: we can promote a user and display both the old rank
/// and new rank". Stack A has the rank as $(old), signals, then promotes; stack B, on the
/// antenna, writes "old $(old)" to the logs a second later. A signalled stack had none of its
/// sender's placeholders.
/// </summary>
public sealed class WiredPlaceholderSignalChainTests
{
    private const int PLAYER_INDEX = 5;
    private const int CLICK_ME = 23;
    private const int ANTENNA = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_signalled_stack_shows_its_senders_placeholder_as_it_read()
    {
        _room.Harness.Fakes.Handlers["Filter"] = call => call.Args[0];
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(ANTENNA, 6, 1);

        var rank = _room.AddBox<WiredVariableUser>(10, 5, 5, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "rank"
            )
        )
            .Should()
            .BeTrue();
        await rank.LoadWiredAsync(Ct);
        await StartAsync(5, 5);

        var rankId = rank.GetVarSnapshot().VariableId;
        (
            await rank.GiveValueAsync(
                new WiredVariableKey(rankId, WiredVariableTargetType.User, PLAYER_INDEX),
                1
            )
        )
            .Should()
            .BeTrue();

        // A: click, $(old) = rank, send signal, then rank + 1.
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredAddonVariablePlaceholder>(2, 0, 0, "wf_xtra_text_output_variable");
        _room.AddBox<WiredActionSendSignal>(3, 0, 0, "wf_act_send_signal");
        _room.AddBox<WiredActionChangeVariable>(4, 0, 0, "wf_act_change_var_val");
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
        await PlaceholderAsync(2, "old", rankId);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [0, 0],
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                4,
                intParams:
                [
                    (int)WiredVariableTargetType.User,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                variableIds: [rankId.ToString()]
            )
        ).Should().BeTrue();

        // B: on the antenna, the log, once A has promoted the user.
        _room.AddBox<WiredTriggerReceiveSignal>(5, 0, 4, "wf_trg_recv_signal");
        _room.AddBox<WiredActionWriteToLogs>(7, 0, 4, "wf_act_log");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                5,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams: [(int)WiredLogLevelType.Info],
                stringParam: "old $(old)",
                definitionSpecifics: [2]
            )
        )
            .Should()
            .BeTrue();

        await StartAsync(0, 0);
        await StartAsync(0, 4);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(6);

        // The user is rank 2 by the time B writes; the placeholder kept what it read when A sent.
        rank.TryGetValue(
                new WiredVariableKey(rankId, WiredVariableTargetType.User, PLAYER_INDEX),
                out var now
            )
            .Should()
            .BeTrue();
        now.Value.Should().Be(2);
        Wired.GetErrorLogs(_now).Select(x => x.ErrorName).Should().Contain("old 1");
    }

    private async Task PlaceholderAsync(int boxId, string name, WiredVariableId variable) =>
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                boxId,
                intParams: [0, (int)WiredVariableTargetType.User, 0],
                stringParam: name,
                variableIds: [variable.ToString()]
            )
        )
            .Should()
            .BeTrue();

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

    /// <summary>
    /// A stack a signal starts reads the users it carried as "Users from signal"; the Variable
    /// placeholder offers that source like every other text add-on (AS3: a merged furni/user
    /// source), and read only the triggering users, of which a signalled stack has none.
    /// </summary>
    [Fact]
    public async Task A_variable_placeholder_reads_the_users_from_the_signal()
    {
        _room.Harness.Fakes.Handlers["Filter"] = call => call.Args[0];
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(ANTENNA, 6, 1);

        var rank = _room.AddBox<WiredVariableUser>(10, 5, 5, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "rank"
            )
        )
            .Should()
            .BeTrue();
        await rank.LoadWiredAsync(Ct);
        await StartAsync(5, 5);

        var rankId = rank.GetVarSnapshot().VariableId;
        (
            await rank.GiveValueAsync(
                new WiredVariableKey(rankId, WiredVariableTargetType.User, PLAYER_INDEX),
                3
            )
        )
            .Should()
            .BeTrue();

        _room.AddBox<WiredTriggerReceiveSignal>(5, 0, 4, "wf_trg_recv_signal");
        _room.AddBox<WiredAddonVariablePlaceholder>(6, 0, 4, "wf_xtra_text_output_variable");
        _room.AddBox<WiredActionWriteToLogs>(7, 0, 4, "wf_act_log");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                5,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                6,
                intParams: [0, (int)WiredVariableTargetType.User, 0],
                stringParam: "rank",
                playerSources:
                [
                    [WiredPlayerSourceType.SignalUsers],
                ],
                variableIds: [rankId.ToString()]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams: [(int)WiredLogLevelType.Info],
                stringParam: "rank $(rank)",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 4);

        await Wired.OnRoomEventAsync(
            new WiredSignalEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                AntennaIds = [ANTENNA],
                FurniIds = [],
                AvatarIds = [PLAYER_INDEX],
                Depth = 1,
            },
            Ct
        );
        await TickAsync(3);

        Wired.GetErrorLogs(_now).Select(x => x.ErrorName).Should().Contain("rank 3");
    }
}
