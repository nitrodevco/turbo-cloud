using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The client asks on every room ready and resolves every group badge it draws through the
/// answer.
///
/// What it needs is the badge of every group represented in the room: the viewer's own groups,
/// the groups the people standing in the room wear, and the room's own group. Anyone who walks
/// in or changes group afterwards is pushed by the room itself.
/// </summary>
public class GetHabboGroupBadgesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetHabboGroupBadgesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetHabboGroupBadgesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var membershipsTask = _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetMembershipsAsync(ct);

        var shownTask =
            ctx.RoomId > 0
                ? _grainFactory.GetRoomGrain(ctx.RoomId).GetShownGuildIdsAsync(ct)
                : Task.FromResult(ImmutableArray<GuildId>.Empty);

        var memberships = await membershipsTask.ConfigureAwait(false);
        var shown = await shownTask.ConfigureAwait(false);

        ImmutableArray<GuildId> guildIds =
        [
            .. memberships.Select(x => x.GroupId).Concat(shown).Distinct(),
        ];

        var guilds = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummariesAsync(guildIds, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new HabboGroupBadgesMessageComposer { Guilds = guilds }, ct)
            .ConfigureAwait(false);
    }
}
