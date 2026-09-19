using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// The badge strip on the infostand and profile.
/// </summary>
public class GetSelectedBadgesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetSelectedBadgesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSelectedBadgesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.PlayerId <= 0)
            return;

        var badges = await _grainFactory
            .GetInventoryGrain(message.PlayerId)
            .GetSelectedBadgesAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new HabboUserBadgesMessageComposer { PlayerId = message.PlayerId, Badges = badges },
                ct
            )
            .ConfigureAwait(false);
    }
}
