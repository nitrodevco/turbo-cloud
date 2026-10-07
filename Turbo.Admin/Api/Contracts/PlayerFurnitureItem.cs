namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One kind of furniture a player owns, by its definition: its class name, <c>floor</c> or
/// <c>wall</c>, and how many they have in their inventory and placed in rooms.
/// </summary>
public sealed record PlayerFurnitureItem(
    int DefinitionId,
    string Name,
    string Type,
    int InInventory,
    int InRooms
);
