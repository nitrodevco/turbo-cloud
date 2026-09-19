using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>A variable was created, written or removed on one target.</summary>
[GenerateSerializer]
public sealed record WiredVariableChangedEvent : RoomEvent
{
    [Id(0)]
    public required WiredVariableId VariableId { get; init; }

    [Id(1)]
    public required WiredVariableTargetType TargetType { get; init; }

    [Id(2)]
    public required int TargetId { get; init; }

    [Id(3)]
    public required WiredVariableChangeType ChangeType { get; init; }

    [Id(4)]
    public required WiredVariableValue Value { get; init; }

    [Id(5)]
    public WiredVariableValue PreviousValue { get; init; }
}
