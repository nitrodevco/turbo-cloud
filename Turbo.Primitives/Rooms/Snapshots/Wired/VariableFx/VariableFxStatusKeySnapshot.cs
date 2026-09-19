using Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

/// <summary>
/// What identifies a shown fx value: the config, the variable feeding it, and the avatar or
/// furni it is drawn over. <see cref="EntityId"/> is a room object id either way.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct VariableFxStatusKeySnapshot(
    [property: Id(0)] int ConfigId,
    [property: Id(1)] WiredVariableId VariableId,
    [property: Id(2)] bool IsUserEntity,
    [property: Id(3)] int EntityId
);
