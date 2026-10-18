using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A water bowl or trough: each drink feeds the pet a little and empties the drink a level. The
/// pet drinks as official horses did (9 of 9, official client capture 2026-10-17, room "saddle
/// club"): it stands there smiling, then drinks ("eat", no gesture) with its drinking line, and
/// smiles again as the drink goes down a level; one pet drinks at a time.
/// </summary>
[RoomObjectLogic("pet_drink")]
public class FurniturePetDrinkLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurniturePetSupplyLogic(stuffDataFactory, ctx)
{
    public override PetSupplyType SupplyType => PetSupplyType.Drink;

    protected override bool IsConsumable => true;

    protected override bool IsBusy => TimerSystem.IsPending(_ctx.ObjectId);

    protected override async Task ServeAsync(IRoomPet pet, int left, CancellationToken ct)
    {
        var config = _roomGrain._petConfig;

        await PetModule.SupplyNutritionAsync(
            pet,
            _ctx.RoomObject.OwnerId,
            Guid.NewGuid().ToString("N"),
            config.DrinkNutrition,
            ct
        );

        if (config.DrinkEnergy > 0)
        {
            pet.SetEnergy(Math.Min(config.MaxEnergy, pet.Energy + config.DrinkEnergy));
            PetModule.Persist(pet);
        }

        pet.AddStatus(AvatarStatusType.Gesture, PetGestures.SMILE);
        pet.ActionExpiresAtMs =
            _roomGrain.NowMs()
            + config.DrinkStartDelayMs
            + config.DrinkDurationMs
            + config.DrinkEndMs;
        // It stays at the drink until it is done: no wandering off mid-drink.
        pet.NextActionAtMs = Math.Max(pet.NextActionAtMs, pet.ActionExpiresAtMs);
        pet.MarkDirty();

        TimerSystem.Schedule(
            _ctx.ObjectId,
            config.DrinkStartDelayMs,
            async drinkCt =>
            {
                if (IsStillHere(pet))
                {
                    pet.RemoveStatus(AvatarStatusType.Gesture);
                    pet.AddStatus(AvatarStatusType.Eat, string.Empty);
                    pet.MarkDirty();

                    await PetModule.SayVocalAsync(pet, "DRINKING", drinkCt);
                }

                TimerSystem.Schedule(
                    _ctx.ObjectId,
                    config.DrinkDurationMs,
                    _ =>
                    {
                        if (IsStillHere(pet))
                        {
                            pet.AddStatus(AvatarStatusType.Gesture, PetGestures.SMILE);
                            pet.MarkDirty();
                        }

                        return ShowLeftAsync(left);
                    }
                );
            }
        );
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    // The pet may have been picked up or sent off while it drank.
    private bool IsStillHere(IRoomPet pet) =>
        pet.ActionExpiresAtMs > 0
        && PetModule.TryGetPet(pet.PetId, out var current)
        && ReferenceEquals(current, pet)
        && !pet.IsWalking;
}
