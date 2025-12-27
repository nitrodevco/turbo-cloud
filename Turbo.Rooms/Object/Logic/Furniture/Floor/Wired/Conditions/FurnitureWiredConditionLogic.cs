using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

public abstract class FurnitureWiredConditionLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredCondition
{
    public override WiredType WiredType => WiredType.Condition;

    public abstract bool Evaluate(IWiredContext ctx);

    public virtual bool IsNegative() => false;

    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataConditionSnapshot()
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
            AdvancedMode = true,
            AmountFurniSelections = [],
            AllowWallFurni = false,
            AllowedFurniSources = GetFurniSources(),
            AllowedUserSources = GetPlayerSources(),
            QuantifierCode = 0,
            QuantifierType = 0,
            IsInvert = false,
        };
}
