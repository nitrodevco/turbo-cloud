using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariableDefinition
{
    public int VariableId { get; }
    public string VariableName { get; }
    public WiredAvailabilityType AvailabilityType { get; }
    public WiredVariableTargetType TargetType { get; }
    public WiredVariableFlags Flags { get; }
    public Dictionary<int, string> TextConnectors { get; }

    public WiredVariableSnapshot GetSnapshot();
}
