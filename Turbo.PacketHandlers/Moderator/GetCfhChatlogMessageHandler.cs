using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class GetCfhChatlogMessageHandler : IMessageHandler<GetCfhChatlogMessage>
{
    public async ValueTask HandleAsync(
        GetCfhChatlogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
