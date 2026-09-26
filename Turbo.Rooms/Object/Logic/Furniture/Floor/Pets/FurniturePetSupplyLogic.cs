using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// Base of the pet supplies. A consumable (food, drink) steps through its states with every
/// serving and is destroyed once the last one is gone; a nest or toy is never used up.
/// </summary>
public abstract class FurniturePetSupplyLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IPetSupplyLogic
{
    public abstract PetSupplyType SupplyType { get; }

    protected virtual bool IsConsumable => false;

    public bool HasSuppliesLeft => !IsConsumable || GetState() < LastState;

    private int LastState => System.Math.Max(0, _ctx.Definition.TotalStates - 1);

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public async Task OnPetReachedAsync(IRoomPet pet, CancellationToken ct)
    {
        if (!HasSuppliesLeft)
            return;

        await ServeAsync(pet, ct);

        if (!IsConsumable)
            return;

        var next = GetState() + 1;

        if (next >= LastState)
        {
            await _roomGrain.ActionModule.DeleteItemByIdAsync(
                ActionContext.CreateForSystem(_ctx.RoomId),
                _ctx.ObjectId,
                ct
            );

            return;
        }

        await SetStateAsync(next);
    }

    protected abstract Task ServeAsync(IRoomPet pet, CancellationToken ct);
}
