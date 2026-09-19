namespace Turbo.Primitives.Pets.Enums;

/// <summary>Codes of <c>PetPlacingError</c>, each mapped to an alert by the client's room users handler.</summary>
public enum PetPlacingErrorType
{
    ForbiddenInHotel = 0,
    ForbiddenInFlat = 1,
    MaxPetsInRoom = 2,
    NoFreeTiles = 3,
    SelectedTileNotFree = 4,
    MaxOwnPets = 5,
}
