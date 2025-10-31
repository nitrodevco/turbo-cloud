using Turbo.Contracts.Enums.Furniture;

namespace Turbo.Primitives.Snapshots.Furniture;

public sealed record FurnitureDefinitionSnapshot(
    int Id,
    int SpriteId,
    string PublicName,
    string ProductName,
    ProductTypeEnum ProductType,
    string LogicName,
    int TotalStates,
    int X,
    int Y,
    double Z,
    bool CanStack,
    bool CanWalk,
    bool CanSit,
    bool CanLay,
    bool CanRecycle,
    bool CanTrade,
    bool CanGroup,
    bool CanSell,
    FurniUsagePolicy UsagePolicy,
    string? ExtraData
);
