using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// "WIRED Variable: Pointer Variable" (<c>wf_var_pointer</c>, VariableCodes 9, AS3
/// <c>PointerVariable</c> WIN63-202610201032): "another variable in disguise, identified through a
/// context variable pointing to the furni ID of the target variable's Wired box"
/// (<c>variables.pointer.usage_info</c>). Int param 0 is the kind of variable it stands for (the
/// editor's header: furni, user, global or context); the one picked variable id is the context
/// accessor variable. While a stack runs, reads go to the variable box whose furni id the
/// accessor holds, when that box makes a variable of the same kind; writes through it change
/// nothing (below).
///
/// It needs a running stack to know its target, so it is listed as requiring an execution context,
/// which keeps it out of the pickers that use a variable outside one
/// (<c>wiredfurni.error.variables.requires_execution_context</c>: "You cannot select a pointer
/// variable in this context."). Listed as Habbo lists it: created, availability 999 (internal), the
/// header's kind, with a value that can be written, requiring an execution context (official client
/// capture lab60, WIN63-202610201032). Inference: that a box of another kind, or another pointer,
/// is no target.
///
/// Writes through a pointer (give, set, take) are no-ops: on Habbo, Change Variable "assign 7"
/// through a furni or a global pointer whose context variable held the target box's furni id left
/// the target unchanged, 8 of 8 fires, while a direct write from the same box worked; reads
/// through it returned the target's value (global 3/3, furni 2/2) (official client capture
/// 2026-10-20, lab60 + lab61, NOTES-lab61-20261020.md).
/// </summary>
[RoomObjectLogic("wf_var_pointer")]
public class WiredVariablePointer(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_TARGET = 0;

    public override int WiredCode => (int)WiredVariableBoxType.Pointer;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.VariableTarget(WiredVariableTargetType.User)];

    // A fresh box opens on users (official client capture lab60, WIN63-202610201032: ints [1]).
    protected override WiredVariableTargetType TargetType =>
        GetIntParamOrDefault(PARAM_TARGET, WiredVariableTargetType.User);

    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Internal;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.RequiresExecutionContext;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        var target = GetTarget();

        return target is not null
            && CanBind(key)
            && target.TryGetValue(Forward(target, key), out value);
    }

    // Writes through a pointer change nothing on Habbo (see the summary).
    public override Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    ) => Task.FromResult(false);

    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    ) => Task.FromResult(false);

    public override bool RemoveValue(WiredVariableKey key) => false;

    /// <summary>
    /// The variable box the accessor points at in the running stack: the accessor's value is its
    /// furni id. Null outside a stack, when the accessor holds no value, or when that furni makes
    /// no variable of this pointer's kind.
    /// </summary>
    private FurnitureWiredVariableLogic? GetTarget()
    {
        if (GetVariable(0) is not { } accessor)
            return null;

        var accessorSnapshot = accessor.GetVarSnapshot();

        if (
            accessorSnapshot.TargetType != WiredVariableTargetType.Context
            || !accessor.TryGetValue(
                new WiredVariableKey(
                    accessorSnapshot.VariableId,
                    WiredVariableTargetType.Context,
                    0
                ),
                out var boxId
            )
            || boxId.Value <= 0
            || boxId.Value > int.MaxValue
            || !FurniModule.TryGetItem((int)boxId.Value, out var item)
            || item.Logic is not FurnitureWiredVariableLogic target
            || target is WiredVariablePointer
        )
            return null;

        return target.GetVarSnapshot().TargetType == TargetType ? target : null;
    }

    private static WiredVariableKey Forward(IWiredVariable target, WiredVariableKey key) =>
        new(target.GetVarSnapshot().VariableId, key.TargetType, key.TargetId);
}
