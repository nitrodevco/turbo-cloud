using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// An addon that sits on a variable box tile and derives extra variables from it. The wired
/// system rebuilds them with the variable boxes, so tile changes publish the variable box
/// event rather than the stack event.
/// </summary>
public abstract class FurnitureWiredSubVariableAddonLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredSubVariableProvider
{
    /// <summary>The variable box sharing this tile, if any.</summary>
    protected FurnitureWiredVariableLogic? GetParentBox()
    {
        var tileIdx = _ctx.GetTileIdx();

        if (tileIdx < 0 || tileIdx >= _roomGrain._state.TileFloorStacks.Length)
            return null;

        foreach (var itemId in _roomGrain._state.TileFloorStacks[tileIdx])
        {
            if (
                _roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
                && item.Logic is FurnitureWiredVariableLogic variable
            )
                return variable;
        }

        return null;
    }

    public IEnumerable<IWiredVariable> GetSubVariables()
    {
        var parent = GetParentBox();

        if (parent is null)
            return [];

        var parentSnapshot = parent.GetVarSnapshot();

        if (string.IsNullOrWhiteSpace(parentSnapshot.VariableName))
            return [];

        return BuildSubVariables(parent, parentSnapshot.VariableName).ToList();
    }

    protected abstract IEnumerable<IWiredVariable> BuildSubVariables(
        FurnitureWiredVariableLogic parent,
        string parentName
    );

    protected WiredSubVariable Create(
        int index,
        string parentName,
        string suffix,
        FurnitureWiredVariableLogic parent,
        Func<IWiredVariable, WiredVariableKey, WiredVariableValue?> compute
    ) =>
        new(
            WiredVariableIdBuilder.CreateFromBoxId(ObjectId, index),
            $"{parentName}.{suffix}",
            parent,
            compute
        );

    protected override Task OnWiredStackChangedAsync(
        ActionContext ctx,
        List<int> ids,
        CancellationToken ct
    ) =>
        _ctx.PublishRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                BoxIds = [_ctx.ObjectId.Value],
            },
            ct
        );
}
