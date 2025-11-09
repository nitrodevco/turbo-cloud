using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;

namespace Turbo.PacketHandlers.Quest;

public class ActivateQuestMessageHandler : IMessageHandler<ActivateQuestMessage>
{
    public async ValueTask HandleAsync(
        ActivateQuestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
