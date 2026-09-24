using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The client asks on every room ready and resolves every group badge it draws through the
/// answer.
///
/// What it needs is the badge of every group represented in the room. Today that is the
/// viewer's own groups and nothing else, because no avatar in a room advertises a group yet —
/// that arrives with the room integration, and this is where the room's own groups join the
/// list when it does.
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
        if (ctx.PlayerId <= 0)
            return;

        var memberships = await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetMembershipsAsync(ct)
            .ConfigureAwait(false);

        var guilds = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummariesAsync([.. memberships.Select(x => x.GroupId)], ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new HabboGroupBadgesMessageComposer { Guilds = guilds }, ct)
            .ConfigureAwait(false);
    }
}
