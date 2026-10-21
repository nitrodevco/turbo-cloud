namespace Turbo.Admin.Api.Contracts;

/// <summary>A pet standing in a room, as staff set it: each value left out stays as the pet has it.</summary>
public sealed record RoomPetRequest(
    string? Name,
    int? Level,
    int? Energy,
    int? Nutrition,
    bool? AnyoneCanRide,
    bool? HasBreedingPermission
);
