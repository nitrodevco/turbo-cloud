using System;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Database.Extensions;

public static class FurnitureDefinitionEntityExtensions
{
    /// <param name="minimumStackHeight">Floor for the stack height, so nothing is zero tall.</param>
    public static FurnitureDefinitionSnapshot ToSnapshot(
        this FurnitureDefinitionEntity entity,
        double minimumStackHeight
    ) =>
        new()
        {
            Id = entity.Id,
            SpriteId = entity.SpriteId,
            Name = entity.Name,
            ProductType = entity.ProductType,
            FurniCategory = entity.FurniCategory,
            LogicName = entity.Logic,
            TotalStates = entity.TotalStates,
            Width = entity.Width,
            Length = entity.Length,
            StackHeight = Math.Round(Math.Max(minimumStackHeight, entity.StackHeight), 2),
            CanStack = entity.CanStack,
            CanWalk = entity.CanWalk,
            CanSit = entity.CanSit,
            CanLay = entity.CanLay,
            CanRecycle = entity.CanRecycle,
            CanTrade = entity.CanTrade,
            CanGroup = entity.CanGroup,
            CanSell = entity.CanSell,
            UsagePolicy = entity.UsagePolicy,
            ExtraData = entity.ExtraData,
        };
}
