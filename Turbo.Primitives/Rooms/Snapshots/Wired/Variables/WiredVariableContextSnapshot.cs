using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public abstract record WiredVariableContextSnapshot
{
    [Id(0)]
    public required WiredContextType ContextType { get; init; }
}
