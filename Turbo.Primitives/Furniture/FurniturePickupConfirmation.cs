using Orleans;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The "are you sure" a player answers before picking up a furni that loses something when it
/// leaves the room (<c>ObjectRemoveConfirm</c>). Both are hotel text keys: the Flash client wraps
/// each in <c>${...}</c> before showing it (<c>RoomMessageHandler</c>), and neither key is read
/// anywhere in the client, so only the server can name them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurniturePickupConfirmation
{
    /// <summary>A wired variable box: picking it up clears every reference to it and what it stored.</summary>
    public static readonly FurniturePickupConfirmation WiredVariable = new()
    {
        TitleKey = "wiredfurni.variables.pickup.title",
        BodyKey = "wiredfurni.variables.pickup.body",
    };

    [Id(0)]
    public required string TitleKey { get; init; }

    [Id(1)]
    public required string BodyKey { get; init; }
}
