using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetGuildCreationInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuildCreationInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuildCreationInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var creationInfo = await _grainFactory
            .GetPlayerGuildGrain(ctx.PlayerId)
            .GetCreationInfoAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new GuildCreationInfoMessageComposer { CreationInfo = creationInfo },
                ct
            )
            .ConfigureAwait(false);
    }
}
