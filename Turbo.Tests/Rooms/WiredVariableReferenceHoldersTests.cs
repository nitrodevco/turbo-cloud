using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Change Variable and Variable Value have two merged selections, each a furni slot and a user
/// slot (AS3 <c>ChangeVariable.mergedSelections</c> <c>[[0,0],[1,1]]</c>, official client capture
/// of their sources: two furni and two user slots, defaults triggering/selector). The first
/// names the holders acted on, the second the holders the reference variable is read on: the
/// triggering user (5) is compared with or takes the "ref" the selected user (6) holds.
/// </summary>
public sealed class WiredVariableReferenceHoldersTests
{
    private const int TRIGGERER = 5;
    private const int SELECTED = 6;
    private const int MARK = 20;
    private const int REF = 21;
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);
    private WiredVariableUser _mark = null!;
    private WiredVariableUser _ref = null!;
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Target user, the operation or comparison, the reference a variable, value 0, on users.
    private static int[] ReferenceParams(int operation) =>
        [
            (int)WiredVariableTargetType.User,
            operation,
            1,
            0,
            0,
            (int)WiredVariableTargetType.User,
            0,
        ];

    [Fact]
    public async Task Change_variable_reads_the_reference_on_its_second_selection()
    {
        await PlaceMarkAsync();
        var change = _room.AddBox<WiredActionChangeVariable>(BOX, 0, 0, "wf_act_change_var_val");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: ReferenceParams((int)WiredVariableOperationType.Set),
                furniSources: Furni(),
                playerSources: Users(),
                definitionSpecifics: [0],
                variableIds: [Id(_mark), Id(_ref)]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(TRIGGERER);
        ctx.SelectorPool.SelectedAvatarIds.Add(SELECTED);

        (await change.ExecuteAsync(ctx, Ct)).Should().BeTrue();
        Value(_mark, TRIGGERER).Should().Be(7);
        Value(_mark, SELECTED).Should().Be(1, "the reference holders are not acted on");
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(1, false)]
    public async Task Variable_value_compares_with_the_reference_on_its_second_selection(
        int selectedHolds,
        bool matches
    )
    {
        await PlaceMarkAsync(selectedHolds);
        var condition = _room.AddBox<WiredConditionVariableValueMatch>(
            BOX,
            0,
            0,
            "wf_cnd_var_val_match"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: ReferenceParams((int)WiredComparisonType.Equals),
                furniSources: Furni(),
                playerSources: Users(),
                definitionSpecifics: [0],
                variableIds: [Id(_mark), Id(_ref)]
            )
        )
            .Should()
            .BeTrue();
        await Give(_mark, TRIGGERER, 7);

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        ctx.Selected.SelectedAvatarIds.Add(TRIGGERER);
        ctx.SelectorPool.SelectedAvatarIds.Add(SELECTED);

        condition.Evaluate(ctx).Should().Be(matches);
    }

    private static string Id(WiredVariableUser variable) =>
        variable.GetVarSnapshot().VariableId.ToString();

    private static WiredFurniSourceType[][] Furni() =>
        [
            [WiredFurniSourceType.TriggeredItem],
            [WiredFurniSourceType.SelectorItems],
        ];

    private static WiredPlayerSourceType[][] Users() =>
        [
            [WiredPlayerSourceType.TriggeredUser],
            [WiredPlayerSourceType.SelectorUsers],
        ];

    private static WiredVariableKey Key(WiredVariableUser variable, int holder) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.User, holder);

    private static long Value(WiredVariableUser variable, int holder)
    {
        variable.TryGetValue(Key(variable, holder), out var value).Should().BeTrue();

        return value.Value;
    }

    private static async Task Give(WiredVariableUser variable, int holder, long value) =>
        (
            await variable.GiveValueAsync(
                Key(variable, holder),
                new WiredVariableValue(value),
                replace: true
            )
        )
            .Should()
            .BeTrue();

    /// <summary>Both users hold "mark" at 1; "ref" is 1 on the triggerer and set on the selected user.</summary>
    private async Task PlaceMarkAsync(int selectedHolds = 7)
    {
        _room.Enter(TRIGGERER, 1, 1);
        _room.Enter(SELECTED, 2, 1);
        _mark = await PlaceVariableAsync(MARK, 6, 6, "mark");
        _ref = await PlaceVariableAsync(REF, 5, 5, "ref");
        await Give(_mark, TRIGGERER, 1);
        await Give(_mark, SELECTED, 1);
        await Give(_ref, TRIGGERER, 1);
        await Give(_ref, SELECTED, selectedHolds);
    }

    private async Task<WiredVariableUser> PlaceVariableAsync(int id, int x, int y, string name)
    {
        var variable = _room.AddBox<WiredVariableUser>(id, x, y, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(x, y)],
                },
                Ct
            );
        _now += 1_000;
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(_now, dormant: false, Ct);

        return variable;
    }
}
