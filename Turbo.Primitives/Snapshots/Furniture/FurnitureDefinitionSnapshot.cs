using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Snapshots.Furniture;

public sealed record FurnitureDefinitionSnapshot(
    int Id,
    int SpriteId,
    string PublicName,
    string ProductName,
    ProductType ProductType,
    string LogicName,
    int TotalStates,
    int Width,
    int Height,
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
