using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredInternalVariable(RoomGrain roomGrain) : IWiredVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    protected WiredVariableSnapshot? _snapshot;

    protected abstract WiredVariableDefinition BuildVariableDefinition();

    public virtual bool CanBind(in IWiredVariableBinding binding) =>
        GetVarSnapshot().TargetType == binding.TargetType;

    public virtual bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        return false;
    }

    public virtual Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    ) => Task.FromResult(false);

    public virtual bool RemoveValue(string key) => false;

    public WiredVariableKey GetVariableKey()
    {
        var snapshot = GetVarSnapshot();

        return new WiredVariableKey(snapshot.TargetType, snapshot.VariableName);
    }

    public WiredVariableSnapshot GetVarSnapshot() =>
        _snapshot ??= BuildVariableDefinition().GetSnapshot();
}
