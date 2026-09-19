using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Bots;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Bots;

public class CommandBotMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CommandBotMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CommandBotMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.BotId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .CommandBotAsync(
                ctx.AsActionContext(),
                message.BotId,
                message.Skill,
                message.Data ?? string.Empty,
                ct
            )
            .ConfigureAwait(false);
    }
}
