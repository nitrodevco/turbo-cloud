using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Main.Hotel;

/// <summary><see cref="IHotelInventory"/> over the furni definitions, the inventory grains and the badge grains.</summary>
internal sealed class HostHotelInventory(
    IGrainFactory grains,
    IFurnitureDefinitionProvider definitions
) : IHotelInventory
{
    public HotelFurniture? FindFurniture(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : ToFurniture(definitions.TryGetDefinitionByName(name.Trim()));

    public HotelFurniture? Furniture(int definitionId) =>
        ToFurniture(definitions.TryGetDefinition(definitionId));

    public async Task<bool> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    ) =>
        definitions.TryGetDefinition(definitionId) is not null
        && await grains
            .GetInventoryGrain(player)
            .GrantFurnitureAsync(definitionId, null, MovementCause.New(cause, player), ct)
            .ConfigureAwait(false)
            is not null;

    public Task<bool> HasBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        grains.GetPlayerBadgeGrain(player).HasBadgeAsync(badgeCode, ct);

    public Task<bool> GiveBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        grains.GetPlayerBadgeGrain(player).GiveBadgeAsync(badgeCode, ct);

    private static HotelFurniture? ToFurniture(FurnitureDefinitionSnapshot? definition) =>
        definition is null
            ? null
            : new(definition.Id, definition.Name, definition.ProductType == ProductType.Wall);
}
