using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Bots;

namespace Turbo.PacketHandlers.Room.Bots;

public class GetBotCommandConfigurationDataMessageHandler
    : IMessageHandler<GetBotCommandConfigurationDataMessage>
{
    public async ValueTask HandleAsync(
        GetBotCommandConfigurationDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
