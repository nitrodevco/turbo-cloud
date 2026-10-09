using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Groupforums;

public class UpdateForumReadMarkerMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateForumReadMarkerMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateForumReadMarkerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerGuildForumGrain(ctx.PlayerId)
            .MarkReadAsync(message.Markers, ct)
            .ConfigureAwait(false);
    }
}
