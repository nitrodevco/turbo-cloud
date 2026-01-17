using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariableDefinition
{
    public string Name { get; set; }
    public WiredVariableTargetType TargetType { get; set; }
    public WiredAvailabilityType AvailabilityType { get; set; }
    public WiredInputSourceType InputSourceType { get; set; }
    public WiredVariableFlags Flags { get; set; }
    public Dictionary<int, string> TextConnectors { get; set; }

    public WiredVariableSnapshot GetSnapshot();
}
