using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A rollback done: the set it made, and what it left alone because that changed again since (a
/// field edited after an import keeps the edit when the import is rolled back).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GamedataRollbackResult
{
    [Id(0)]
    public required GamedataChangeSetSnapshot ChangeSet { get; init; }

    [Id(1)]
    public required ImmutableArray<string> Skipped { get; init; }
}
