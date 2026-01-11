using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariableDefinition
{
    public string Key { get; }
    public string Name { get; }

    public WiredVariableTargetType Target { get; }
    public WiredAvailabilityType AvailabilityType { get; }
    public WiredInputSourceType InputSourceType { get; }
    public WiredVariableFlags Flags { get; }
    public List<string> TextConnectors { get; }

    public int GetHashCode();
    public WiredVariableSnapshot GetSnapshot();
}
