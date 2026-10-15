using System;
using System.Collections.Generic;
using System.Globalization;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Gives the variable box on the same tile a label per value. The string param holds one
/// "value=text" pair per line; the variable box reads them when it builds its snapshot. A
/// "_=text" line is the fallback, shown for every value without a label of its own (Wired
/// Faculty, 12/10/2026: "Fallback text value in Text connectors"; the client shows it under
/// the key "_" in the overview and for an unmapped value, AS3 <c>Util.getConnectedText</c>).
/// </summary>
[RoomObjectLogic("wf_xtra_var_text_connector")]
public class WiredAddonVariableTextConnector(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    // The editor's text area takes 1000 characters (VariableTextConnectorAddon.buildInputs).
    private const int TEXT_MAX_LENGTH = 1000;

    public override int WiredCode => (int)WiredAddonType.VARIABLE_TEXT_CONVERTER;

    protected override int GetStringParamMaxLength() => TEXT_MAX_LENGTH;

    /// <summary>The text of the box's "_=" line, or null when it has none.</summary>
    public string? GetFallback()
    {
        string? fallback = null;

        foreach (var line in Lines())
        {
            var index = line.IndexOf('=');

            if (index <= 0 || line[..index].Trim() != "_")
                continue;

            var label = line[(index + 1)..].Trim();

            if (label.Length > 0)
                fallback = label;
        }

        return fallback;
    }

    /// <summary>
    /// The value of a line with no text ("0="), which an empty capture stands for: a trailing
    /// exact-mode marker the player left out (official client capture). Null without one. Such a
    /// line labels nothing, so it is not among <see cref="GetConnectors"/>.
    /// </summary>
    public WiredVariableValue? GetEmptyTextValue()
    {
        foreach (var line in Lines())
        {
            var index = line.IndexOf('=');

            if (
                index > 0
                && line[(index + 1)..].Trim().Length == 0
                && int.TryParse(
                    line[..index].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
                return new WiredVariableValue(value);
        }

        return null;
    }

    private string[] Lines() =>
        (_wiredData?.StringParam ?? string.Empty).Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries
        );

    /// <summary>The value to label map the box currently describes.</summary>
    public Dictionary<WiredVariableValue, string> GetConnectors()
    {
        var connectors = new Dictionary<WiredVariableValue, string>();
        if (_wiredData is null)
            return connectors;

        foreach (var line in Lines())
        {
            var index = line.IndexOf('=');

            if (index <= 0)
                continue;

            if (
                !int.TryParse(
                    line[..index].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
                continue;

            var label = line[(index + 1)..].Trim();

            if (label.Length > 0)
                connectors[new WiredVariableValue(value)] = label;
        }

        return connectors;
    }
}
