using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class JoinHabboGroupMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<JoinHabboGroupMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        JoinHabboGroupMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .JoinAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (result.Failure is { } reason)
        {
            await ctx.SendComposerAsync(
                    new HabboGroupJoinFailedMessageComposer { Reason = reason },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        // A request to an exclusive group is not recorded; the refresh redraws the window the
        // player is looking at, which reads the group's details.
        if (result.Status != GuildMembershipStatus.Member)
        {
            await ctx.SendComposerAsync(
                    new GroupDetailsChangedMessageComposer { GuildId = message.GuildId },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        // Habbo answers a join with the joiner's groups and the group's details, then wears the
        // group if they wore none and says their profile changed (official client capture
        // 2026-10-13 20:55 and 21:07, both joins: WiredPermissions, RaidProtectionCapability,
        // GuildMemberships, HabboGroupDetails with favourite still false, HabboGroupBadges,
        // FavoriteMembershipUpdate, ExtendedProfileChanged). The rights come from the homeroom,
        // the badge from the room they stand in. The details redraw the join button the window
        // disabled when it was clicked (GroupDetailsCtrl.as:175).
        var playerGuilds = _grainFactory.GetPlayerGuildGrain(ctx.PlayerId);
        var guilds = await playerGuilds.GetMembershipsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new GuildMembershipsMessageComposer { Guilds = guilds }, ct)
            .ConfigureAwait(false);
        await ctx.SendHabboGroupDetailsAsync(_grainFactory, message.GuildId, false, ct)
            .ConfigureAwait(false);
        await playerGuilds.WearIfNoneAsync(message.GuildId, ct).ConfigureAwait(false);

        // Through the player's presence, not the session: it follows the room's badge messages,
        // which the wear only told the room to send (IPlayerPresenceGrain.HoldForRoomAsync).
        await _grainFactory
            .SendComposerToPlayerAsync(
                ctx.PlayerId,
                new ExtendedProfileChangedMessageComposer { UserId = ctx.PlayerId.Value },
                ct
            )
            .ConfigureAwait(false);
    }
}
