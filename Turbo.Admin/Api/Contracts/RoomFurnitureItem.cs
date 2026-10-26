namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A furniture item in a room: what it is (its definition and the logic the server runs for it),
/// whose it is, where it stands and the data it holds.
/// </summary>
public sealed record RoomFurnitureItem(
    int Id,
    int DefinitionId,
    string ClassName,
    string? Name,
    int SpriteId,
    bool Wall,
    string Logic,
    int OwnerId,
    string OwnerName,
    int X,
    int Y,
    double Z,
    int Rotation,
    string? ExtraData
);
