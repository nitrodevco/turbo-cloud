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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// Mirrors another variable under a new name: reads and writes go to the source. The source
/// is the single picked variable id; the box takes its target type.
/// </summary>
[RoomObjectLogic("wf_var_echo")]
public class WiredVariableEcho(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Echo;

    public override int GetMaxVariableIds() => 1;

    protected override WiredVariableTargetType TargetType =>
        GetSource()?.GetVarSnapshot().TargetType ?? WiredVariableTargetType.None;

    protected override WiredAvailabilityType AvailabilityType =>
        GetSource()?.GetVarSnapshot().AvailabilityType ?? WiredAvailabilityType.Unknown;

    protected override WiredVariableFlags Flags =>
        GetSource()?.GetVarSnapshot().Flags ?? WiredVariableFlags.None;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        var source = GetSource();

        return source is not null
            && CanBind(key)
            && source.TryGetValue(Forward(source, key), out value);
    }

    public override Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        var source = GetSource();

        return source is null || !CanBind(key)
            ? Task.FromResult(false)
            : source.GiveValueAsync(Forward(source, key), value, replace);
    }

    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        var source = GetSource();

        return source is null || !CanBind(key)
            ? Task.FromResult(false)
            : source.SetValueAsync(ctx, Forward(source, key), value);
    }

    public override bool RemoveValue(WiredVariableKey key)
    {
        var source = GetSource();

        return source is not null && CanBind(key) && source.RemoveValue(Forward(source, key));
    }

    public override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        GetSource()?.GetVarSnapshot().TextConnectors ?? [];

    private IWiredVariable? GetSource()
    {
        var source = GetVariable(0);

        return source is WiredVariableEcho ? null : source;
    }

    private static WiredVariableKey Forward(IWiredVariable source, WiredVariableKey key) =>
        new(source.GetVarSnapshot().VariableId, key.TargetType, key.TargetId);
}
