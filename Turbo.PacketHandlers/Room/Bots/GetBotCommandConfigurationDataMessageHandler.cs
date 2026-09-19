using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Bots;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Bots;

public class GetBotCommandConfigurationDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetBotCommandConfigurationDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetBotCommandConfigurationDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.BotId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RequestBotCommandConfigurationAsync(
                ctx.AsActionContext(),
                message.BotId,
                message.Skill,
                ct
            )
            .ConfigureAwait(false);
    }
}
