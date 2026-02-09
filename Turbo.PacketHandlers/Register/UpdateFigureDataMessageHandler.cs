using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Register;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

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

        await player
            .SetFigureAsync(
                message.Figure,
                AvatarGenderTypeExtensions.FromLegacyString(message.Gender),
                ct
            )
            .ConfigureAwait(false);
    }
}
