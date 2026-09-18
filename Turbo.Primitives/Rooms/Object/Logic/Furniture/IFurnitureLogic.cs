using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
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
    public bool CanToggle();
    public Altitude GetStackHeight();
    public int GetState();
    public string GetLegacyString();
    public int GetNextToggleableState();
    public int GetPrevToggleableState();
    public Task SetStateAsync(int state, bool refresh = true);
    public Task OnStateChangedAsync(CancellationToken ct);
    public Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct);
    public Task OnPlaceAsync(ActionContext ctx, CancellationToken ct);
    public Task OnPickupAsync(ActionContext ctx, CancellationToken ct);
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
