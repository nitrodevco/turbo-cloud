using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// What "WIRED Variable: From Another Room" offers (<c>SharedVariableList</c>): every permanent,
/// shared user or global variable in the other rooms of the box's owner, with the room it is in.
/// </summary>
[GenerateSerializer, Immutable]
public record WiredVariableSharedListSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required ImmutableArray<WiredVariableSharedSnapshot> Variables { get; init; }
}
