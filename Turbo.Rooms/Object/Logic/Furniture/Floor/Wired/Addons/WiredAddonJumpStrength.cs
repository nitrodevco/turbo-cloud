using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Bends the movements of the stack into an arc: users and furni the actions move jump to
/// their tile instead of sliding. The client draws the arc; the room only sends how high, as
/// the jump power of a user move and the curve strength of a furni move.
///
/// Int params, as the client's editor writes them: value or variable, the value (one int, not
/// a long), the variable's target. The one variable id is the variable the height is read
/// from, on the first selected target that holds it.
/// </summary>
[RoomObjectLogic("wf_xtra_mov_curve")]
public class WiredAddonJumpStrength(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_USE_VARIABLE = 0;
    private const int PARAM_VALUE = 1;
    private const int PARAM_TARGET = 2;

    // The bounds and the default of the editor's number input.
    private const int MIN_STRENGTH = -1000;
    private const int MAX_STRENGTH = 1000;
    private const int DEFAULT_STRENGTH = 80;

    public override int WiredCode => (int)WiredAddonType.JUMP_STRENGTH;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(MIN_STRENGTH, MAX_STRENGTH, DEFAULT_STRENGTH),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        long strength = GetIntParamOrDefault(PARAM_VALUE, DEFAULT_STRENGTH);

        // A variable nobody selected holds leaves the movement as it was.
        if (
            GetIntParamOrDefault(PARAM_USE_VARIABLE, false)
            && !TryReadVariableOperand(0, PARAM_TARGET, ctx.Selected, out strength)
        )
            return Task.FromResult(true);

        // A variable can hold anything; the client's input cannot.
        ctx.Policy.JumpStrength = (int)Math.Clamp(strength, MIN_STRENGTH, MAX_STRENGTH);

        return Task.FromResult(true);
    }
}
