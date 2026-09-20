namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// What kind of furni this is, as the wired <c>@type</c> variable reports it: an ordinary one
/// somebody owns, one the Builders Club lends while a membership lasts, or one the room made
/// up and will forget (<see cref="Object.Furniture.IRoomItem.IsTemporary"/>).
/// </summary>
public enum FurnitureOwnershipType
{
    Normal = 0,

    /// <summary>Lent by the Builders Club while a membership lasts; nobody owns it.</summary>
    BuildersClub = 1,
    Temporary = 2,
}
