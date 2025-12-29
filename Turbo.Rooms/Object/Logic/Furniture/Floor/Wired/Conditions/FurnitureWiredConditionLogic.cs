using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
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

    private int _quantifierCode;
    private byte _quantifierType;
    private bool _isInvert;

    public override List<object> GetDefinitionSpecifics() =>
        [.. base.GetDefinitionSpecifics(), _quantifierCode];

    public override List<object> GetTypeSpecifics() =>
        [.. base.GetTypeSpecifics(), _quantifierType, _isInvert];

    public abstract bool Evaluate(IWiredContext ctx);

    public virtual bool IsNegative() => false;
}
