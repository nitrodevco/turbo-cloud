using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// Base of the pet supplies; a nest or toy is never used up. A consumable (food, drink) shows how
/// much is left in its state, its highest state full, and goes down one with every serving; an
/// empty one stays in the room. Official troughs went 4, 3, 2, 1 and a water bowl 4, 3, 2 with the
/// drinks, none was removed in 9 minutes, and a hay bucket stood at 0 (official client capture
/// 2026-10-17, room "saddle club"). One left alone for <c>SupplyRefillMs</c>, or not served
/// since the room loaded, is full again at its next serving.
/// </summary>
public abstract class FurniturePetSupplyLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IPetSupplyLogic
{
    // Room clock of the last serving; none yet since the room loaded.
    private long? _lastServedAtMs;

    public abstract PetSupplyType SupplyType { get; }

    protected virtual bool IsConsumable => false;

    public bool HasSuppliesLeft =>
        !IsBusy && (!IsConsumable || FullState > 0 && (IsRefillDue || GetState() > 0));

    /// <summary>A pet is still being served here; the next waits until it is done.</summary>
    protected virtual bool IsBusy => false;

    private int FullState => System.Math.Max(0, _ctx.Definition.TotalStates - 1);

    private bool IsRefillDue
    {
        get
        {
            if (_lastServedAtMs is not { } servedAt)
                return true;

            var refillMs = _roomGrain._petConfig.SupplyRefillMs;

            return refillMs > 0 && _roomGrain.NowMs() - servedAt >= refillMs;
        }
    }

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public async Task OnPetReachedAsync(IRoomPet pet, CancellationToken ct)
    {
        if (!HasSuppliesLeft)
            return;

        var left = -1;

        if (IsConsumable)
        {
            left = (IsRefillDue ? FullState : GetState()) - 1;
            _lastServedAtMs = _roomGrain.NowMs();
        }

        await ServeAsync(pet, left, ct);
    }

    /// <summary>
    /// Serves the pet; <paramref name="left"/> is the state a consumable is left at once it is
    /// done (shown with <see cref="ShowLeftAsync"/>), -1 for one that is never used up.
    /// </summary>
    protected abstract Task ServeAsync(IRoomPet pet, int left, CancellationToken ct);

    protected Task ShowLeftAsync(int left) =>
        left < 0 ? Task.CompletedTask : SetStateAsync(System.Math.Max(0, left));
}
