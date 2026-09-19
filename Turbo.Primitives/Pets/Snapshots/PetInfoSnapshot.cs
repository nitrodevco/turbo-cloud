using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>Everything the pet info stand shows, in the client's own field vocabulary.</summary>
[GenerateSerializer, Immutable]
public sealed record PetInfoSnapshot
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int Level { get; init; }

    [Id(3)]
    public required int MaxLevel { get; init; }

    [Id(4)]
    public required int Experience { get; init; }

    [Id(5)]
    public required int ExperienceRequiredToLevel { get; init; }

    [Id(6)]
    public required int Energy { get; init; }

    [Id(7)]
    public required int MaxEnergy { get; init; }

    [Id(8)]
    public required int Nutrition { get; init; }

    [Id(9)]
    public required int MaxNutrition { get; init; }

    [Id(10)]
    public required int Respect { get; init; }

    [Id(11)]
    public required PlayerId OwnerId { get; init; }

    [Id(12)]
    public required int AgeDays { get; init; }

    [Id(13)]
    public required string OwnerName { get; init; }

    [Id(14)]
    public required int BreedId { get; init; }

    [Id(15)]
    public required bool HasFreeSaddle { get; init; }

    [Id(16)]
    public required bool IsRiding { get; init; }

    [Id(17)]
    public required ImmutableArray<int> SkillThresholds { get; init; }

    /// <summary><see cref="PetRiding.ACCESS_ANYONE"/> when anyone may ride.</summary>
    [Id(18)]
    public required int AccessRights { get; init; }

    [Id(19)]
    public required bool CanBreed { get; init; }

    [Id(20)]
    public required bool CanHarvest { get; init; }

    [Id(21)]
    public required bool CanRevive { get; init; }

    [Id(22)]
    public required int RarityLevel { get; init; }

    [Id(23)]
    public required int MaxWellBeingSeconds { get; init; }

    [Id(24)]
    public required int RemainingWellBeingSeconds { get; init; }

    [Id(25)]
    public required int RemainingGrowingSeconds { get; init; }

    [Id(26)]
    public required bool HasBreedingPermission { get; init; }
}
