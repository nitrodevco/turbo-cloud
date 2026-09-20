using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
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

    private int _quantifierCode = 0;

    /// <summary>
    /// What this box's shared quantifier radio counts. The server declares it and the client
    /// only draws it; <see cref="WiredQuantifierType.None"/> hides the radio, which is why the
    /// quantifier of most boxes stays zero.
    /// </summary>
    protected virtual WiredQuantifierType QuantifierType => WiredQuantifierType.None;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int)];

    // Nothing is stored: the client sends the quantifier code back and nothing else.
    public override List<object> GetTypeSpecifics() =>
        [.. base.GetTypeSpecifics(), (byte)QuantifierType, IsNegative()];

    public int GetQuantifierCode() => _quantifierCode;

    public bool GetIsInvert() => IsNegative();

    public byte GetQuantifierType() => (byte)QuantifierType;

    /// <summary>The "not" boxes derive from their positive twin and flip this.</summary>
    public virtual bool IsNegative() => false;

    /// <summary>
    /// Evaluates the box. A negative box flips the outcome of <see cref="EvaluateCore"/>, so a
    /// concrete condition only states the positive rule.
    /// </summary>
    public bool Evaluate(IWiredProcessingContext ctx)
    {
        bool result;

        try
        {
            result = EvaluateCore(ctx);
        }
        catch (Exception ex)
        {
            _roomGrain.WiredSystem.RecordError(
                ex.GetType().Name,
                Grains.Systems.RoomWiredSystem.GetErrorCategory(this),
                _roomGrain.NowMs()
            );

            _roomGrain._logger.LogWarning(
                ex,
                "Wired condition {WiredCode} failed in room {RoomId}",
                WiredCode,
                _roomGrain.RoomId
            );

            result = false;
        }

        return IsNegative() ? !result : result;
    }

    protected virtual bool EvaluateCore(IWiredProcessingContext ctx) => false;

    /// <summary>
    /// Folds per-target outcomes with the box quantifier: zero requires every target to
    /// match, anything else is satisfied by one. An empty set never matches.
    /// </summary>
    protected bool Quantify(IEnumerable<bool> results, bool requireAll)
    {
        var any = false;

        foreach (var result in results)
        {
            if (requireAll && !result)
                return false;

            any |= result;
        }

        return any;
    }

    /// <summary>The client "require all" radio, stored as int param 0 on most conditions.</summary>
    protected bool RequiresAll(int paramIndex = 0) => GetIntParamOrDefault(paramIndex, false);

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _quantifierCode = _wiredData.GetDefinitionParam<int>(0);
    }
}
