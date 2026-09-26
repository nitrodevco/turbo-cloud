using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Shared by the bot actions: the string param names the bot (for the talk and dress boxes
/// the name is the first tab-separated field), and the user slot is labelled "bots" by the
/// client. The named bot is resolved at execution time.
/// </summary>
public abstract class FurnitureWiredBotActionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    protected const char FIELD_SEPARATOR = '\t';

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.BotByName];

    /// <summary>
    /// The two slots of a bot box that also acts on users (follow, hand item, talk to): the
    /// users first, then the bot by name.
    /// </summary>
    protected static List<WiredPlayerSourceType[]> UserAndBotSources() =>
        [WiredSources.Users, WiredSources.BotByName];

    /// <summary>
    /// Whether the box works with no bot named. Only the hand item editor lets the bot be left
    /// out (its "bot.usage" checkbox sends an empty name); every other bot editor asks for one.
    /// </summary>
    protected virtual bool IsBotOptional => false;

    /// <summary>The bot name and the remaining text of a tab separated string param.</summary>
    protected (string botName, string text) SplitParam()
    {
        var raw = _wiredData.StringParam ?? string.Empty;
        var index = raw.IndexOf(FIELD_SEPARATOR);

        return index < 0
            ? (raw.Trim(), string.Empty)
            : (raw[..index].Trim(), raw[(index + 1)..].Trim());
    }

    protected bool TryGetBot(string name, out IRoomBot bot) =>
        _roomGrain.BotModule.TryGetBotByName(name, out bot);

    /// <summary>
    /// The bot the box names, by the box's own rule: false when a bot is needed and none by
    /// that name is in the room. A box whose bot is optional and unnamed resolves to null.
    /// </summary>
    protected bool TryResolveBot(string name, out IRoomBot? bot)
    {
        bot = null;

        if (name.Length == 0)
            return IsBotOptional;

        if (!TryGetBot(name, out var found))
            return false;

        bot = found;

        return true;
    }
}
