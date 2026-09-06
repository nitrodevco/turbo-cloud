using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Register;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Register;

public class UpdateFigureDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateFigureDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateFigureDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId < 0)
            return;

        var player = _grainFactory.GetPlayerGrain(ctx.PlayerId);

        await player.SetFigureAsync(message.Figure, message.Gender, ct).ConfigureAwait(false);
    }
}
