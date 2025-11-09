using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Lobby;

namespace Turbo.PacketHandlers.Game.Lobby;

public class class_165MessageHandler : IMessageHandler<class_165Message>
{
    public async ValueTask HandleAsync(
        class_165Message message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
