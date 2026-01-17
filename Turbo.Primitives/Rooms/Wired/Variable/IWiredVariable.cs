using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable
{
    public int VariableId { get; set; }
    public string VariableName { get; }
    public IStorageData StorageData { get; }

    public WiredVariableTargetType GetVariableTargetType();
    public WiredAvailabilityType GetVariableAvailabilityType();
    public WiredInputSourceType GetVariableInputSourceType();
    public WiredVariableFlags GetVariableFlags();
    public Dictionary<int, string> GetTextConnectors();

    public bool CanBind(in IWiredVariableBinding binding);
    public bool TryGet(in IWiredVariableBinding binding, out int value);
    public Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    );
    public bool RemoveValue(string key);
    public WiredVariableSnapshot GetVarSnapshot();
}
