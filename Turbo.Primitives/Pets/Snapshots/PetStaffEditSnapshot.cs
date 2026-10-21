using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>
/// A placed pet as staff set it from the admin panel. Each value left null is left as the pet has
/// it, so a value the panel read before the pet changed it (energy falls on its own) is not
/// written back.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetStaffEditSnapshot
{
    [Id(0)]
    public string? Name { get; init; }

    /// <summary>Ignored for a monsterplant, which grows with time.</summary>
    [Id(1)]
    public int? Level { get; init; }

    [Id(2)]
    public int? Energy { get; init; }

    [Id(3)]
    public int? Nutrition { get; init; }

    [Id(4)]
    public bool? AnyoneCanRide { get; init; }

    [Id(5)]
    public bool? HasBreedingPermission { get; init; }
}
