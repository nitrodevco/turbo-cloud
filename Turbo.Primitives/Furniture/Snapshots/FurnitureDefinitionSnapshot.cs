using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Furniture.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureDefinitionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required string Name { get; init; }

    [Id(3)]
    public required ProductType ProductType { get; init; }

    [Id(4)]
    public required FurnitureCategory FurniCategory { get; init; }

    [Id(5)]
    public required string LogicName { get; init; }

    [Id(6)]
    public required int TotalStates { get; init; }

    [Id(7)]
    public required int Width { get; init; }

    [Id(8)]
    public required int Length { get; init; }

    [Id(9)]
    public required Altitude StackHeight { get; init; }

    [Id(10)]
    public required bool CanStack { get; init; }

    [Id(11)]
    public required bool CanWalk { get; init; }

    [Id(12)]
    public required bool CanSit { get; init; }

    [Id(13)]
    public required bool CanLay { get; init; }

    [Id(14)]
    public required bool CanRecycle { get; init; }

    [Id(15)]
    public required bool CanTrade { get; init; }

    [Id(16)]
    public required bool CanGroup { get; init; }

    [Id(17)]
    public required bool CanSell { get; init; }

    /// <summary>
    /// The inventory's sellable flag (Flash <c>FurniView</c> shows "Sell in Marketplace" only
    /// for it) and what the marketplace takes: furni this hotel lets be sold
    /// (<see cref="CanSell"/>) that can be traded and is not credit furni. Every official
    /// inventory recorded (72 kinds) sends an untradeable item as unsellable (18 of 18) and of
    /// the tradeable ones only the credit furni (the bronze coin) as unsellable.
    /// </summary>
    public bool IsSellable => CanSell && CanTrade && !CreditFurniValue.TryParse(Name, out _);

    /// <summary>
    /// The inventory's recyclable flag (Flash <c>FurniData</c>): furni this hotel lets be
    /// recycled (<see cref="CanRecycle"/>) that can be traded. Every official inventory recorded
    /// (69 kinds) follows it; the recyclable but untradeable ones (bed_silo_two_notrd,
    /// usva_nt_shelf, guild_forum, ...) arrive as not recyclable.
    /// </summary>
    public bool IsRecyclable => CanRecycle && CanTrade;

    [Id(18)]
    public required FurnitureUsageType UsagePolicy { get; init; }

    [Id(19)]
    public required string? ExtraData { get; init; }

    /// <summary>
    /// The furnidata's <c>customparams</c>: what a type means by it is its own (a sound set's
    /// number, a roller's speed, a building block's height step).
    /// </summary>
    [Id(20)]
    public string? CustomParams { get; init; }

    /// <summary>The furnidata <c>name</c>: what players call the furni ("Small Block").</summary>
    [Id(21)]
    public string? PublicName { get; init; }

    /// <summary>
    /// The name to show players: <see cref="PublicName"/>, or the class name (<see cref="Name"/>)
    /// when the hotel's furnidata has none.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(PublicName) ? Name : PublicName;
}
