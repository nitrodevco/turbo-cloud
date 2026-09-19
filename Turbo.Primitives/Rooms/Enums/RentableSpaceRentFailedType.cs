namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// Why a rentable space cannot be rented, as the client numbers it. The status message carries
/// one of these too, and the client reads <see cref="None"/> there as "can rent".
/// </summary>
public enum RentableSpaceRentFailedType
{
    None = 0,
    AlreadyRented = 100,
    NotRented = 101,
    NotRentedByYou = 102,
    CanRentOnlyOneSpace = 103,
    NotEnoughCredits = 200,
    NotEnoughDuckets = 201,
    NoPermission = 202,
    NoHabboClub = 203,
    Disabled = 300,
    Generic = 400,
}
