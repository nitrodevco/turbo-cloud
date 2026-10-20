using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// What one user, pet or bot holds of the room's permanent user variables, as Variable
/// Management shows it (AS3 <c>WiredUserPermanentVariablesList</c>). A pet or bot also names its
/// owner.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredEntityPermanentVariablesSnapshot
{
    /// <summary>1 user, 2 pet, 4 bot (<c>wiredfurni.params.usertype.&lt;n&gt;</c>).</summary>
    [Id(0)]
    public required int EntityType { get; init; }

    [Id(1)]
    public required int EntityId { get; init; }

    [Id(2)]
    public required string EntityName { get; init; }

    [Id(3)]
    public required string EntityFigure { get; init; }

    [Id(4)]
    public int OwnerId { get; init; }

    [Id(5)]
    public string OwnerName { get; init; } = string.Empty;

    [Id(6)]
    public string OwnerFigure { get; init; } = string.Empty;

    [Id(7)]
    public required ImmutableArray<WiredStoredVariableValueSnapshot> Variables { get; init; }
}
