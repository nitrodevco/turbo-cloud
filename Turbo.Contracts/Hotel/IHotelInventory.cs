using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Hotel;

/// <summary>A furni definition: its id, its class name (<c>throne</c>) and whether it stands on the floor or hangs on a wall.</summary>
public sealed record HotelFurniture(int DefinitionId, string Name, bool IsWallItem);

/// <summary>What players hold: furni and badges.</summary>
public interface IHotelInventory
{
    /// <summary>The definition of that class name (any case), or null when the hotel has none.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    HotelFurniture? FindFurniture(string name);

    /// <summary>The definition of that id, or null.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    HotelFurniture? Furniture(int definitionId);

    /// <summary>
    /// Makes one furni of the definition in the player's inventory (online or not), recorded in
    /// the movement log under <paramref name="cause"/>, a code of the plugin's. Not keyed: call it
    /// once per thing given. False when there is no such definition.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    );

    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> HasBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct);

    /// <summary>Gives the player the badge; false when they had it already.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> GiveBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct);
}
