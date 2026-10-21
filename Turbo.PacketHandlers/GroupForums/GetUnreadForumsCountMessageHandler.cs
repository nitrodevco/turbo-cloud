using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class GetUnreadForumsCountMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetUnreadForumsCountMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetUnreadForumsCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerGuildForumGrain(ctx.PlayerId)
            .SendUnreadForumsCountAsync(ct)
            .ConfigureAwait(false);
    }
}
