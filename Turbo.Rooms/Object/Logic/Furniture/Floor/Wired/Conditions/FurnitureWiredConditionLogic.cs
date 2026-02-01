using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

public abstract class FurnitureWiredConditionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredCondition
{
    public override WiredType WiredType => WiredType.Condition;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int), typeof(bool)];

    public override List<Type> GetTypeSpecificTypes() =>
        [.. base.GetTypeSpecificTypes(), typeof(byte)];

    public int GetQuantifierCode()
    {
        var quantifierCode = 0;

        try
        {
            if (_wiredData.DefinitionSpecifics is not null)
            {
                quantifierCode = (int)_wiredData.DefinitionSpecifics[0]!;
            }
        }
        catch { }

        return quantifierCode;
    }

    public bool GetIsInvert()
    {
        var isInvert = false;

        try
        {
            if (_wiredData.DefinitionSpecifics is not null)
            {
                isInvert = (bool)_wiredData.DefinitionSpecifics[1]!;
            }
        }
        catch { }

        return isInvert;
    }

    public byte GetQuantifierType()
    {
        var quantifierType = (byte)0;

        try
        {
            if (_wiredData.TypeSpecifics is not null)
            {
                quantifierType = (byte)_wiredData.TypeSpecifics[0]!;
            }
        }
        catch { }

        return quantifierType;
    }

    public virtual bool IsNegative() => false;

    public virtual bool Evaluate(IWiredProcessingContext ctx) => false;
}
