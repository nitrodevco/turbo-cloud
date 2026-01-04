using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

public abstract class FurnitureWiredConditionLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
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
            if (WiredData.DefinitionSpecifics is not null)
            {
                quantifierCode = (int)WiredData.DefinitionSpecifics[0]!;
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
            if (WiredData.DefinitionSpecifics is not null)
            {
                isInvert = (bool)WiredData.DefinitionSpecifics[1]!;
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
            if (WiredData.TypeSpecifics is not null)
            {
                quantifierType = (byte)WiredData.TypeSpecifics[0]!;
            }
        }
        catch { }

        return quantifierType;
    }

    public virtual bool Evaluate(WiredProcessingContext ctx) => false;

    public virtual bool IsNegative() => false;
}
