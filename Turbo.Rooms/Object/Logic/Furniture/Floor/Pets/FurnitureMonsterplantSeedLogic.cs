using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>
/// A monsterplant seed. The owner double-clicks it to plant: a new monsterplant of a breed
/// rolled by rarity (from <see cref="MonsterplantSeedData.MinRarityLevel"/> up) sprouts where
/// the seed stood and the seed is gone.
/// </summary>
[RoomObjectLogic("monsterplant_seed")]
public class FurnitureMonsterplantSeedLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    // The client offers planting it to its owner only, and sends a plain use; Nobody keeps the
    // use button away from everyone else, and this lets the owner's use through.
    public override Task<bool> CanUseAsync(ActionContext ctx) => Task.FromResult(IsItemOwner(ctx));

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var data = FurnitureExtraDataSections.Read<MonsterplantSeedData>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            MonsterplantSeedData.SECTION,
            _roomGrain._logger
        );

        await _roomGrain.PetModule.PlantSeedAsync(
            ctx,
            _ctx.RoomObject,
            data?.MinRarityLevel ?? 0,
            ct
        );
    }
}
