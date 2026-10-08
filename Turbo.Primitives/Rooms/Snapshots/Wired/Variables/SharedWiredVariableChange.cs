using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// One change to a shared variable, between the room it lives in and the rooms that use it. The
/// holder is the player id for a user variable and 0 for a global. A give that overwrites an
/// existing value is <see cref="WiredVariableChangeType.Created"/> with <see cref="Replace"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SharedWiredVariableChange
{
    [Id(0)]
    public required WiredVariableChangeType ChangeType { get; init; }

    [Id(1)]
    public required int HolderId { get; init; }

    [Id(2)]
    public required long Value { get; init; }

    [Id(3)]
    public long Previous { get; init; }

    [Id(4)]
    public bool Replace { get; init; }
}
