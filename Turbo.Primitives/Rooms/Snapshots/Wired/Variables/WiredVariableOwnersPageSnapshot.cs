using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// One page of who holds a permanent user variable, as Variable Management lists them (AS3
/// <c>WiredUserVariablesPage</c>): how many match the user type filter, the page and its size,
/// the holders, and the filter and sort it was made with.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredVariableOwnersPageSnapshot
{
    [Id(0)]
    public required string VariableId { get; init; }

    [Id(1)]
    public required int TotalEntries { get; init; }

    [Id(2)]
    public required int Page { get; init; }

    [Id(3)]
    public required int PageSize { get; init; }

    [Id(4)]
    public required ImmutableArray<WiredVariableOwnerSnapshot> Owners { get; init; }

    /// <summary><c>wiredfurni.params.usertype.&lt;n&gt;</c>, -1 for every kind.</summary>
    [Id(5)]
    public required int UserTypeFilter { get; init; }

    /// <summary><c>wiredmenu.variable_management.sort_by.&lt;n&gt;</c>.</summary>
    [Id(6)]
    public required int SortType { get; init; }
}

/// <summary>One holder on a Variable Management page (AS3 <c>WiredUserVariablesElement</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record WiredVariableOwnerSnapshot
{
    /// <summary>1 user, 2 pet, 4 bot (<c>wiredfurni.params.usertype.&lt;n&gt;</c>).</summary>
    [Id(0)]
    public required int EntityType { get; init; }

    /// <summary>The player, pet or bot id.</summary>
    [Id(1)]
    public required int EntityId { get; init; }

    [Id(2)]
    public required string EntityName { get; init; }

    [Id(3)]
    public required WiredStoredVariableValueSnapshot Value { get; init; }
}

/// <summary>
/// A held value with when it was given and last changed (AS3 <c>WiredVariableStorageParameter</c>),
/// in epoch ms and as the menu prints them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredStoredVariableValueSnapshot
{
    [Id(0)]
    public required string VariableId { get; init; }

    [Id(1)]
    public required int Value { get; init; }

    [Id(2)]
    public required long CreatedAtMs { get; init; }

    [Id(3)]
    public required string CreatedAtText { get; init; }

    [Id(4)]
    public required long UpdatedAtMs { get; init; }

    [Id(5)]
    public required string UpdatedAtText { get; init; }
}
