using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Commands.Enums;

namespace Turbo.Primitives.Commands.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CommandTreeParameterSnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required CommandParameterKind Kind { get; init; }

    [Id(2)]
    public required bool Optional { get; init; }

    [Id(3)]
    public required CommandSuggestType Suggest { get; init; }

    /// <summary>An enumeration's members; empty for any other kind.</summary>
    [Id(4)]
    public required ImmutableArray<string> Members { get; init; }

    /// <summary>A player parameter the player may aim at <c>@room</c> and <c>@online</c>.</summary>
    [Id(5)]
    public required bool Selectors { get; init; }

    [Id(6)]
    public string Description { get; init; } = string.Empty;

    [Id(7)]
    public string Minimum { get; init; } = string.Empty;

    [Id(8)]
    public string Maximum { get; init; } = string.Empty;

    [Id(9)]
    public int MinLength { get; init; } = -1;

    [Id(10)]
    public int MaxLength { get; init; } = -1;

    [Id(11)]
    public string DefaultValue { get; init; } = string.Empty;
}
