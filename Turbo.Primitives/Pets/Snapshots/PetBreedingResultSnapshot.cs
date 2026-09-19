using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>What one owner got out of a monsterplant breeding: the seed item and its rarity.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedingResultSnapshot
{
    [Id(0)]
    public required int StuffId { get; init; }

    [Id(1)]
    public required int ClassId { get; init; }

    [Id(2)]
    public required string ProductCode { get; init; }

    [Id(3)]
    public required PlayerId OwnerId { get; init; }

    [Id(4)]
    public required string OwnerName { get; init; }

    [Id(5)]
    public required int RarityLevel { get; init; }

    [Id(6)]
    public required bool HasMutation { get; init; }
}
