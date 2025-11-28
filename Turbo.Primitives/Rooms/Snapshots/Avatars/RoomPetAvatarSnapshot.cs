using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomPetAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(0)]
    public required int SubType { get; init; }

    [Id(1)]
    public required int OwnerId { get; init; }

    [Id(2)]
    public required string OwnerName { get; init; }

    [Id(3)]
    public required int RarityLevel { get; init; }

    [Id(4)]
    public required bool HasSaddle { get; init; }

    [Id(5)]
    public required bool IsRiding { get; init; }

    [Id(6)]
    public required bool CanBreed { get; init; }

    [Id(7)]
    public required bool CanHarvest { get; init; }

    [Id(8)]
    public required bool CanRevive { get; init; }

    [Id(9)]
    public required bool HasBreedingPermission { get; init; }

    [Id(10)]
    public required bool PetLevel { get; init; }

    [Id(11)]
    public required bool PetPosture { get; init; }
}
