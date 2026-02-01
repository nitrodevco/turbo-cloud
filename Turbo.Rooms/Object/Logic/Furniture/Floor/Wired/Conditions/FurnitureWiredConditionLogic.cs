using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

public abstract class FurnitureWiredConditionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredCondition
{
    public override WiredType WiredType => WiredType.Condition;

    private int _quantifierCode = 0;
    private bool _isInvert = false;
    private byte _quantifierType = 0;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int), typeof(bool)];

    public override List<Type> GetTypeSpecificTypes() =>
        [.. base.GetTypeSpecificTypes(), typeof(byte)];

    public int GetQuantifierCode() => _quantifierCode;

    public bool GetIsInvert() => _isInvert;

    public byte GetQuantifierType() => _quantifierType;

    public virtual bool IsNegative() => false;

    public virtual bool Evaluate(IWiredProcessingContext ctx) => false;

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            _quantifierCode = _wiredData.GetDefinitionParam<int>(0);
            _isInvert = _wiredData.GetDefinitionParam<bool>(1);
            _quantifierType = _wiredData.GetTypeParam<byte>(0);
        }
        catch { }
    }
}
