using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Commands.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CommandSyntaxSnapshot
{
    [Id(0)]
    public required string Path { get; init; }

    [Id(1)]
    public required string Usage { get; init; }

    [Id(2)]
    public required ImmutableArray<CommandTreeParameterSnapshot> Parameters { get; init; }
}
