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
/// accessor variable. While a stack runs, reads and writes go to the variable box whose furni id
/// the accessor holds, when that box makes a variable of the same kind.
///
/// It needs a running stack to know its target, so it is listed as requiring an execution context,
/// which keeps it out of the pickers that use a variable outside one
/// (<c>wiredfurni.error.variables.requires_execution_context</c>: "You cannot select a pointer
/// variable in this context."). Inference: that a box of another kind, or another pointer, is no
/// target, and the flags it is listed with (it may stand for any variable of its kind).
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
        [WiredRules.VariableTarget(WiredVariableTargetType.Furni)];

    protected override WiredVariableTargetType TargetType =>
        GetIntParamOrDefault(PARAM_TARGET, WiredVariableTargetType.Furni);

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

    public override Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        var target = GetTarget();

        return target is null || !CanBind(key)
            ? Task.FromResult(false)
            : target.GiveValueAsync(Forward(target, key), value, replace);
    }

    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        var target = GetTarget();

        return target is null || !CanBind(key)
            ? Task.FromResult(false)
            : target.SetValueAsync(ctx, Forward(target, key), value);
    }

    public override bool RemoveValue(WiredVariableKey key)
    {
        var target = GetTarget();

        return target is not null && CanBind(key) && target.RemoveValue(Forward(target, key));
    }

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
