using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
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

    protected const int SLOT_BOTS = 0;
    protected const int SLOT_USERS = 1;

    /// <summary>
    /// The bots slot: the bot named in the box by default, or the bots among the triggering,
    /// selector or signal users (official client capture of the bot boxes' sources, user slot 0
    /// titled sources.users.title.bots in AS3 1318).
    /// </summary>
    protected static WiredPlayerSourceType[] BotSources =>
        [
            WiredPlayerSourceType.BotByName,
            WiredPlayerSourceType.TriggeredUser,
            WiredPlayerSourceType.SelectorUsers,
            WiredPlayerSourceType.SignalUsers,
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [BotSources];

    /// <summary>
    /// The second slot of a bot box that also acts on users (follow, hand item, talk to): the
    /// users, defaulting to the triggering one ("Select users source:", allowed [0,200,201],
    /// default 0; official client capture 2026-10-22T22-54-01-Open).
    /// </summary>
    protected static WiredPlayerSourceType[] UserSources => WiredSources.Users;

    /// <summary>The users of the second slot, for the boxes that act on users too.</summary>
    protected IWiredSelectionSet TargetUsers(IWiredContext ctx) =>
        WiredSlotSelection.ForUserSlot(this, ctx, SLOT_USERS);

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        // Boxes saved while the users came first and the bot second keep what they did.
        if (
            GetAllowedPlayerSources().Count == 2
            && _wiredData.PlayerSources is [var users, var bots]
            && bots.Contains(WiredPlayerSourceType.BotByName)
            && !users.Contains(WiredPlayerSourceType.BotByName)
        )
        {
            _wiredData.PlayerSources = [bots, users];
            _wiredData.MarkDirty();
            InvalidateSnapshot();
        }
    }

    /// <summary>
    /// Whether the box acts on one bot of those its name matches rather than on all of them:
    /// follow, hand item and talk to avatar pick one, talk, move, teleport and change figure
    /// act on every bot of that name (official client capture 2026-10-24, two bots "Frank" and
    /// "frank"; follow start and stop each moved one bot, a different one between runs).
    /// </summary>
    protected virtual bool ActsOnOneBot => false;

    /// <summary>
    /// The bot name, as saved (a space before or after it is part of the name), and the
    /// remaining text of a tab separated string param.
    /// </summary>
    protected (string botName, string text) SplitParam()
    {
        var raw = _wiredData.StringParam ?? string.Empty;
        var index = raw.IndexOf(FIELD_SEPARATOR);

        return index < 0 ? (raw, string.Empty) : (raw[..index], raw[(index + 1)..].Trim());
    }

    /// <summary>
    /// The bots a box acts on when its bot slot can also be a user source: the bots named in
    /// the box for "Use the bot specified by name" (one of them for <see cref="ActsOnOneBot"/>),
    /// otherwise the bots among that slot's users (the triggering or reached user, a
    /// selector's or a signal's).
    /// </summary>
    protected List<IRoomBot> ResolveBots(IWiredContext ctx, int slot, string name)
    {
        if (IsBotByName(slot))
        {
            var named = BotModule.GetBotsByName(name);

            return ActsOnOneBot && named.Count > 1
                ? [named[Random.Shared.Next(named.Count)]]
                : named;
        }

        var users = WiredSlotSelection.ForUserSlot(this, ctx, slot).SelectedAvatarIds;

        return [.. BotModule.Bots.Where(bot => users.Contains(bot.ObjectId))];
    }

    /// <summary>Whether the bots of <paramref name="slot"/> are the one named in the box.</summary>
    protected bool IsBotByName(int slot)
    {
        var sources = GetPlayerSources();

        return slot >= sources.Count || sources[slot].Contains(WiredPlayerSourceType.BotByName);
    }
}
