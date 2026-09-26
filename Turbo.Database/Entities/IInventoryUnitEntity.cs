namespace Turbo.Database.Entities;

/// <summary>
/// A pet or a bot row: owned by a player, and in their inventory while
/// <see cref="RoomEntityId"/> is null. Lets the inventory load, hand over and delete both with
/// one piece of code instead of two copies that drift.
/// </summary>
public interface IInventoryUnitEntity
{
    public int Id { get; }
    public int PlayerEntityId { get; }
    public int? RoomEntityId { get; set; }
}
