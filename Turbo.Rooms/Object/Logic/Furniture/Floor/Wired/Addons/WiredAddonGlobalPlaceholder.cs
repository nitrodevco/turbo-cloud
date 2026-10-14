using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// A text placeholder for the whole room (Flash <c>AddonCodes.GLOBAL_PLACEHOLDER</c> 2000,
/// editor <c>wired_setup.addons.GlobalPlaceholderAddon</c>): "$(name)" in any wired text of the
/// room becomes the add-on's value.
/// <para>
/// The editor writes [type, 0, room id] and "name\tvalue": type 0 "from value" with the fixed
/// text (up to 100 characters), type 1 "from another room" with the room id and the name of a
/// placeholder that room shares (the editor's room and placeholder dropdowns come from the
/// wired context's shared placeholder list). This server shares no placeholders between rooms,
/// so a type 1 add-on has no value and its token reads as nothing.
/// </para>
/// That it reaches every stack of the room, not only its own, is inferred from "global" (Habbo's
/// global add-ons, such as "Variable Global Add-on: Web API", are room-wide) and from the
/// other room's placeholders being shared by name: the box is not in Habbo's furnidata yet. The
/// classname is this server's; a hotel may also point a definition's logic column at it.
/// </summary>
[RoomObjectLogic("wf_xtra_global_placeholder")]
public class WiredAddonGlobalPlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredTextPlaceholder
{
    private const char SIGIL = '$';
    private const char SEPARATOR = '\t';

    private const int PARAM_TYPE = 0;
    private const int TYPE_FROM_VALUE = 0;
    private const int TYPE_FROM_ANOTHER_ROOM = 1;

    // The editor's limits: PlaceholderNameSection (32) and TextInputParam("", 100).
    private const int NAME_MAX_LENGTH = 32;
    private const int VALUE_MAX_LENGTH = 100;

    public override int WiredCode => (int)WiredAddonType.GLOBAL_PLACEHOLDER;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(TYPE_FROM_VALUE, TYPE_FROM_ANOTHER_ROOM, TYPE_FROM_VALUE),
            new WiredRangeParamRule(0, 0, 0),
            WiredRules.NonNegative(),
        ];

    public string Token =>
        Name is { Length: > 0 } name ? WiredPlaceholderText.Token(SIGIL, name) : string.Empty;

    /// <summary>The placeholder's name, the string param's first part.</summary>
    public string Name
    {
        get
        {
            var name = Split().name.Trim();

            return name.Length > NAME_MAX_LENGTH ? name[..NAME_MAX_LENGTH] : name;
        }
    }

    /// <summary>What the token becomes: the fixed text, or nothing for another room's placeholder.</summary>
    public string Value
    {
        get
        {
            if (GetIntParamOrDefault(PARAM_TYPE, TYPE_FROM_VALUE) != TYPE_FROM_VALUE)
                return string.Empty;

            var value = Split().value;

            return value.Length > VALUE_MAX_LENGTH ? value[..VALUE_MAX_LENGTH] : value;
        }
    }

    public Task<string> ApplyAsync(IWiredExecutionContext ctx, string text, CancellationToken ct)
    {
        var token = Token;

        return Task.FromResult(
            token.Length == 0 ? text : text.Replace(token, Value, StringComparison.Ordinal)
        );
    }

    private (string name, string value) Split()
    {
        var stringParam = _wiredData.StringParam ?? string.Empty;
        var index = stringParam.IndexOf(SEPARATOR);

        return index < 0
            ? (stringParam, string.Empty)
            : (stringParam[..index], stringParam[(index + 1)..]);
    }
}
