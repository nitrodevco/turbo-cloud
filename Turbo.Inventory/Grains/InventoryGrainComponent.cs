using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Inventory.Grains;

/// <summary>
/// Anything that lives inside one inventory grain and works through it: the section modules.
/// It names what they use the way the grain's own partials do — the owner, their presence, the
/// config, the sibling modules — so a call reads <c>Presence.SendComposerAsync(...)</c> rather
/// than <c>_inventoryGrain.Presence.SendComposerAsync(...)</c>. The same design as the room
/// grain's <c>RoomGrainComponent</c>.
/// <para>
/// A new module on <see cref="InventoryGrain"/>, or a new member the modules share, gets its
/// shorthand here; this is the one list.
/// </para>
/// </summary>
internal abstract class InventoryGrainComponent(InventoryGrain inventoryGrain)
{
    protected readonly InventoryGrain _inventoryGrain = inventoryGrain;

    protected PlayerId PlayerId => _inventoryGrain.PlayerId;
    protected InventoryConfig Config => _inventoryGrain._inventoryConfig;
    protected IWordFilter WordFilter => _inventoryGrain._wordFilter;

    protected IGrainFactory GrainFactory => _inventoryGrain._grainFactory;
    protected IPlayerPresenceGrain Presence => _inventoryGrain.Presence;
    protected IPlayerUnseenItemsGrain UnseenItems => _inventoryGrain.UnseenItems;

    protected InventoryFurniModule FurniModule => _inventoryGrain.FurniModule;
    protected InventoryPetModule PetModule => _inventoryGrain.PetModule;
    protected InventoryBotModule BotModule => _inventoryGrain.BotModule;

    protected ValueTask<string> GetOwnerNameAsync(CancellationToken ct) =>
        _inventoryGrain.GetOwnerNameAsync(ct);
}
