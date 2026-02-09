using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Avatar;

public class ChangeMottoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ChangeMottoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ChangeMottoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId < 0)
            return;

        var player = _grainFactory.GetPlayerGrain(ctx.PlayerId);

        await player.SetMottoAsync(message.Text, ct).ConfigureAwait(false);
    }
}
