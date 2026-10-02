using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Commands.Snapshots;

/// <summary>The commands one player may use, sorted by name: the whole set each time.</summary>
[GenerateSerializer, Immutable]
public sealed record CommandTreeSnapshot
{
    public static readonly CommandTreeSnapshot EMPTY = new() { Commands = [] };

    [Id(0)]
    public required ImmutableArray<CommandTreeEntrySnapshot> Commands { get; init; }
}
