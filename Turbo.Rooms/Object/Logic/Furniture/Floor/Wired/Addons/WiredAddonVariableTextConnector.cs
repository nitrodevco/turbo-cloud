using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Gives the variable box on the same tile a label per value. The string param holds one
/// "value=text" pair per line; the variable box reads them when it builds its snapshot.
/// </summary>
[RoomObjectLogic("wf_xtra_var_text_connector")]
public class WiredAddonVariableTextConnector(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_TEXT_CONVERTER;

    /// <summary>The value to label map the box currently describes.</summary>
    public Dictionary<WiredVariableValue, string> GetConnectors()
    {
        var connectors = new Dictionary<WiredVariableValue, string>();
        if (_wiredData is null)
            return connectors;

        var text = _wiredData.StringParam ?? string.Empty;

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = line.IndexOf('=');

            if (index <= 0)
                continue;

            if (!int.TryParse(line[..index].Trim(), out var value))
                continue;

            var label = line[(index + 1)..].Trim();

            if (label.Length > 0)
                connectors[new WiredVariableValue(value)] = label;
        }

        return connectors;
    }
}
