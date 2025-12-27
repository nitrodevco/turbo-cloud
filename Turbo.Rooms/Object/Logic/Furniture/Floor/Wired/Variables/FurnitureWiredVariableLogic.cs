using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredVariable
{
    public override WiredType WiredType => WiredType.Variable;

    public abstract void Apply(IWiredContext ctx);

    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataVariableSnapshot()
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
