using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class GetThreadsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetThreadsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetThreadsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GroupId <= 0)
            return;

        await _grainFactory
            .GetGuildForumGrain(GuildId.Parse(message.GroupId))
            .SendThreadsAsync(ctx.PlayerId, message.StartIndex, message.Amount, ct)
            .ConfigureAwait(false);
    }
}
