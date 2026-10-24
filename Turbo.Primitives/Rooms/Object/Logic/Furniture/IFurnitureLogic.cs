using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureLogic<out TObject, out TLogic, out TContext>
    : IRoomObjectLogic<TObject, TLogic, TContext>,
        IFurnitureLogic
    where TObject : IRoomItem<TObject, TLogic, TContext>
    where TContext : IRoomItemContext<TObject, TLogic, TContext>
    where TLogic : IFurnitureLogic<TObject, TLogic, TContext>
{
    new TContext Context { get; }
}

public interface IFurnitureLogic : IRoomObjectLogic, IRollableObject
{
    new IRoomItemContext Context { get; }
    public IStuffData StuffData { get; }
    public FurnitureUsageType GetUsagePolicy();

    /// <summary>Whether this player may use the item now; see the implementation's note.</summary>
    public Task<bool> CanUseAsync(ActionContext ctx);
    public bool CanToggle();

    /// <summary>
    /// Whether a player's use of the item does anything at all; false answers the use with
    /// nothing, no state update and no "Furni Is Used".
    /// </summary>
    public bool HasUse();

    /// <summary>
    /// Whether wired may change this item's state now. False leaves it out of the wired state
    /// update altogether (a wired door someone stands in).
    /// </summary>
    public bool CanChangeState();

    /// <summary>
    /// Whether the Toggle Furni State effect switches this item to its next state. False leaves
    /// it out of what the effect changes (a floor switch).
    /// </summary>
    public bool CanWiredToggle();

    /// <summary>
    /// Whether the item tells the room itself when a use happened, instead of the use request
    /// counting as one: a use that first walks the avatar over happens on arrival, or not at all
    /// (a floor switch).
    /// </summary>
    public bool ReportsOwnUse();
    public Altitude GetStackHeight();
    public int GetState();
    public string GetLegacyString();
    public int GetNextToggleableState();
    public int GetPrevToggleableState();
    public Task SetStateAsync(int state, bool refresh = true);
    public Task OnStateChangedAsync(CancellationToken ct);
    public Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct);

    /// <summary>
    /// What the catalogue's purchase parameter makes of a new item before it is first placed (a
    /// Builders Club placement sends it): a recolorable's colour. Most furni take nothing from it.
    /// </summary>
    public void ApplyCatalogParam(string extraParam);
    public Task OnPlaceAsync(ActionContext ctx, CancellationToken ct);
    public Task OnPickupAsync(ActionContext ctx, CancellationToken ct);

    /// <summary>
    /// What the player must confirm before this item is picked up, or null when it goes without
    /// asking. The room asks once and picks it up when the client answers with the confirm flag.
    /// </summary>
    public FurniturePickupConfirmation? GetPickupConfirmation();
    public Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct);

    /// <summary>
    /// A dedicated furniture action (dice, wheel, one-way door). Returns false when this item
    /// does not respond to the interaction or the request is not valid right now.
    /// </summary>
    public Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    );

    /// <summary>Replaces the legacy data string (post-it colour and text) and persists it.</summary>
    public Task SetLegacyDataAsync(string data, bool refresh = true);

    /// <summary>
    /// Merges entries into map-backed stuff data. Returns false when this item's data is not a
    /// map.
    /// </summary>
    public Task<bool> SetMapDataAsync(
        IReadOnlyDictionary<string, string> entries,
        bool refresh = true
    );
    public Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct);
}
