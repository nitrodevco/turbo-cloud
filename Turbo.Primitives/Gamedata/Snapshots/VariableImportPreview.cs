using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What importing a client config would do, before anything is written.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableImportPreview
{
    [Id(0)]
    public required int Added { get; init; }

    [Id(1)]
    public required int Updated { get; init; }

    [Id(2)]
    public required int Unchanged { get; init; }

    /// <summary>
    /// Keys left out: those the hotel writes itself (its gamedata addresses) and those too long
    /// to keep.
    /// </summary>
    [Id(3)]
    public required ImmutableArray<string> Skipped { get; init; }

    [Id(4)]
    public required ImmutableArray<VariableImportItem> Items { get; init; }

    [Id(5)]
    public required bool Truncated { get; init; }

    /// <summary>
    /// The hotel's variables the config lacks, removed when asked; empty otherwise. Listed up to
    /// the preview's limit, <see cref="RemovedCount"/> counting them all. One that follows a
    /// setting or a file, or one of the reception's (<c>landing.view.*</c>), is never among them.
    /// </summary>
    [Id(6)]
    public ImmutableArray<string> Removed { get; init; } = [];

    /// <summary>Every variable the import would remove, listed or not.</summary>
    [Id(7)]
    public int RemovedCount { get; init; }
}
