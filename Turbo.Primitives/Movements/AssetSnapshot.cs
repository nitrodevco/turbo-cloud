using Orleans;

namespace Turbo.Primitives.Movements;

/// <summary>What it would take to make a furni again, carried by the movement that creates or destroys it.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetSnapshot(
    [property: Id(0)] int DefinitionId,
    [property: Id(1)] string? ExtraData
);
