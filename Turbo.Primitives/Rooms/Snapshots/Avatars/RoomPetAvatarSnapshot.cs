using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomPetAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(12)]
    public required int SubType { get; init; }

    [Id(13)]
    public required int OwnerId { get; init; }

    [Id(14)]
    public required string OwnerName { get; init; }

    [Id(16)]
    public required int RarityLevel { get; init; }

    [Id(17)]
    public required bool HasSaddle { get; init; }

    [Id(18)]
    public required bool IsRiding { get; init; }

    [Id(19)]
    public required bool CanBreed { get; init; }

    [Id(20)]
    public required bool CanHarvest { get; init; }

    [Id(21)]
    public required bool CanRevive { get; init; }

    [Id(22)]
    public required bool HasBreedingPermission { get; init; }

    [Id(23)]
    public required bool PetLevel { get; init; }

    [Id(24)]
    public required bool PetPosture { get; init; }
}
