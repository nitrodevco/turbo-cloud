using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Mysterybox;

namespace Turbo.PacketHandlers.Mysterybox;

public class MysteryBoxWaitingCanceledMessageHandler
    : IMessageHandler<MysteryBoxWaitingCanceledMessage>
{
    public async ValueTask HandleAsync(
        MysteryBoxWaitingCanceledMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
