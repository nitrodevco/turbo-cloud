using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class GetHabboGroupDetailsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetHabboGroupDetailsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetHabboGroupDetailsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        await ctx.SendHabboGroupDetailsAsync(
                _grainFactory,
                message.GuildId,
                message.OpenDetails,
                ct
            )
            .ConfigureAwait(false);
    }
}
