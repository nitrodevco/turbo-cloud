using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetGuildMembershipsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuildMembershipsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuildMembershipsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var guilds = await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetMembershipsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new GuildMembershipsMessageComposer { Guilds = guilds }, ct)
            .ConfigureAwait(false);
    }
}
