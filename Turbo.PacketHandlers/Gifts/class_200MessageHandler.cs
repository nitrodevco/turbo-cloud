using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Gifts;

namespace Turbo.PacketHandlers.Gifts;

public class class_200MessageHandler : IMessageHandler<class_200Message>
{
    public async ValueTask HandleAsync(
        class_200Message message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
