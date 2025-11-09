using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class CancelQuestMessageHandler : IMessageHandler<CancelQuestMessage>
{
    public async ValueTask HandleAsync(
        CancelQuestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
