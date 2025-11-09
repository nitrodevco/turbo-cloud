using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Bots;

namespace Turbo.PacketHandlers.Room.Bots;

public class CommandBotMessageHandler : IMessageHandler<CommandBotMessage>
{
    public async ValueTask HandleAsync(
        CommandBotMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
