using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

public abstract class FurnitureWiredAddonLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredAddon
{
    public override WiredType WiredType => WiredType.Addon;

    public abstract Task<bool> MutatePolicyAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task BeforeEffectsAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task AfterEffectsAsync(IWiredContext ctx, CancellationToken ct);

    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataAddonSnapshot()
        {
            FurniLimit = _furniLimit,
            StuffIds = WiredData.StuffIds,
            StuffTypeId = _ctx.Definition.SpriteId,
            Id = _ctx.ObjectId,
            StringParam = WiredData.StringParam,
            IntParams = WiredData.IntParams,
            VariableIds = WiredData.VariableIds,
            FurniSourceTypes = WiredData.FurniSources,
            UserSourceTypes = WiredData.PlayerSources,
            Code = WiredCode,
            AdvancedMode = false,
            AmountFurniSelections = [],
            AllowWallFurni = false,
            AllowedFurniSources = GetFurniSources(),
            AllowedUserSources = GetPlayerSources(),
        };
}
