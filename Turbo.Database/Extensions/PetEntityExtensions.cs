using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Extensions;

/// <summary>Row to snapshot, shared by the inventory (unplaced pets) and rooms (placed pets).</summary>
public static class PetEntityExtensions
{
    public static PetSnapshot ToSnapshot(this PetEntity entity, string ownerName) =>
        new()
        {
            Id = entity.Id,
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            OwnerName = ownerName,
            RoomId = entity.RoomEntityId is { } roomId ? new RoomId(roomId) : null,
            Name = entity.Name,
            Figure = new PetFigureSnapshot
            {
                TypeId = entity.TypeId,
                PaletteId = entity.PaletteId,
                Color = entity.Color,
                BreedId = entity.BreedId,
                CustomParts = PetFigure.ParseCustomParts(entity.CustomParts),
            },
            Level = entity.Level,
            Experience = entity.Experience,
            Energy = entity.Energy,
            Nutrition = entity.Nutrition,
            Respect = entity.Respect,
            RarityLevel = entity.RarityLevel,
            HasSaddle = entity.HasSaddle,
            AnyoneCanRide = entity.AnyoneCanRide,
            HasBreedingPermission = entity.HasBreedingPermission,
            X = entity.X,
            Y = entity.Y,
            Z = entity.Z,
            Rotation = entity.Rotation == Rotation.None ? Rotation.North : entity.Rotation,
            CreatedAtUtc = entity.CreatedAt,
            WateredAtUtc = entity.WateredAt,
            HarvestedAtUtc = entity.HarvestedAt,
        };

    public static PetBreedSnapshot ToSnapshot(this PetBreedEntity entity) =>
        new()
        {
            TypeId = entity.TypeId,
            BreedId = entity.BreedId,
            PaletteId = entity.PaletteId,
            RarityLevel = entity.RarityLevel,
            Sellable = entity.Sellable,
            Rare = entity.Rare,
            ColorTag = entity.ColorTag,
        };
}
