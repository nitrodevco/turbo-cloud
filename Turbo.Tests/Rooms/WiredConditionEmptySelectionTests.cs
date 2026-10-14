using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A condition whose quantifier is "all of them match" (code 0,
/// <c>wiredfurni.params.quantifier.furni.0</c>) holds when its selection is empty, and its "not"
/// twin ("any of the furni does not match", <c>quantifier.furni.neg.0</c>) does not, as an
/// official client capture of Has Variable and Variable Value shows. Furni 30 holds "level" 5,
/// furni 31 holds nothing.
/// </summary>
public sealed class WiredConditionEmptySelectionTests
{
    private const int LEVEL_BOX = 10;
    private const int BOX = 2;
    private const int HOLDER = 30;
    private const int EMPTY = 31;
    private const int FURNI = (int)WiredVariableTargetType.Furni;

    private readonly WiredRoom _room = new(8, 8);
    private WiredVariableFurni _level = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string LevelId => _level.GetVarSnapshot().VariableId.ToString();

    [Fact]
    public async Task Has_variable_holds_over_an_empty_selection()
    {
        var condition = await HasVariableAsync<WiredConditionHasVariable>("wf_cnd_has_var");

        condition.Evaluate(Context()).Should().BeTrue();
        condition.Evaluate(Context(HOLDER)).Should().BeTrue();
        condition.Evaluate(Context(HOLDER, EMPTY)).Should().BeFalse();
    }

    [Fact]
    public async Task Not_has_variable_does_not_hold_over_an_empty_selection()
    {
        var condition = await HasVariableAsync<WiredNegativeConditionHasVariable>(
            "wf_cnd_not_has_var"
        );

        condition.Evaluate(Context()).Should().BeFalse();
        condition.Evaluate(Context(HOLDER, EMPTY)).Should().BeTrue();
    }

    [Fact]
    public async Task Variable_value_holds_over_an_empty_selection()
    {
        await BuildAsync();
        var condition = _room.AddBox<WiredConditionVariableValueMatch>(
            BOX,
            0,
            0,
            "wf_cnd_var_val_match"
        );

        // target, comparison (equals), literal, value (hi, lo) 5, reference target
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [FURNI, (int)WiredComparisonType.Equals, 0, 0, 5, FURNI],
                definitionSpecifics: [0],
                variableIds: [LevelId]
            )
        )
            .Should()
            .BeTrue();

        condition.Evaluate(Context()).Should().BeTrue();
        condition.Evaluate(Context(HOLDER)).Should().BeTrue();
        condition.Evaluate(Context(HOLDER, EMPTY)).Should().BeFalse();
    }

    private async Task<T> HasVariableAsync<T>(string interaction)
        where T : FurnitureWiredConditionLogic
    {
        await BuildAsync();
        var condition = _room.AddBox<T>(BOX, 0, 0, interaction);

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [FURNI],
                // The triggering furni: Has Variable defaults to its picks.
                furniSources:
                [
                    [WiredFurniSourceType.TriggeredItem],
                ],
                definitionSpecifics: [0],
                variableIds: [LevelId]
            )
        )
            .Should()
            .BeTrue();

        return condition;
    }

    private async Task BuildAsync()
    {
        _room.AddFloorItem(HOLDER, 1, 5);
        _room.AddFloorItem(EMPTY, 2, 5);
        _level = _room.AddBox<WiredVariableFurni>(LEVEL_BOX, 4, 4, "wf_var_furni");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                LEVEL_BOX,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "level"
            )
        )
            .Should()
            .BeTrue();

        var wired = _room.Harness.Module<RoomWiredSystem>();

        await wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [LEVEL_BOX],
            },
            Ct
        );
        await wired.ProcessWiredAsync(11_000, dormant: false, Ct);

        (
            await _level.GiveValueAsync(
                new WiredVariableKey(
                    _level.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Furni,
                    HOLDER
                ),
                5,
                true
            )
        )
            .Should()
            .BeTrue();
    }

    private WiredProcessingContext Context(params int[] furni)
    {
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
        ctx.Selected.SelectedFurniIds.UnionWith(furni);

        return ctx;
    }
}
