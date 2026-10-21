namespace Turbo.Admin.Api.Contracts;

/// <summary>A pet standing in a room: what it is, whose it is and how it is doing.</summary>
public sealed record RoomPetItem(
    int Id,
    string Name,
    int TypeId,
    int PaletteId,
    string Color,
    bool IsMonsterplant,
    int OwnerId,
    string OwnerName,
    int Level,
    int Energy,
    int Nutrition,
    bool AnyoneCanRide,
    bool HasBreedingPermission
);
