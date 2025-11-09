using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class class_735MessageHandler : IMessageHandler<class_735Message>
{
    public async ValueTask HandleAsync(
        class_735Message message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
