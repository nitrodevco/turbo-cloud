namespace Turbo.Contracts.Enums.Rooms.Furniture;

public enum RentableSpaceRentFailedType
{
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
