using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Furniture.Snapshots;

public sealed record FurnitureDefinitionSnapshot(
    int Id,
    int SpriteId,
    string PublicName,
    ProductType ProductType,
    FurnitureCategory FurniCategory,
    string LogicName,
    int TotalStates,
    int Width,
    int Length,
    double StackHeight,
    bool CanStack,
    bool CanWalk,
    bool CanSit,
    bool CanLay,
    bool CanRecycle,
    bool CanTrade,
    bool CanGroup,
    bool CanSell,
    FurnitureUsageType UsagePolicy,
    string? ExtraData
);
