using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;

namespace Turbo.PacketHandlers.Camera;

public class PurchasePhotoMessageHandler : IMessageHandler<PurchasePhotoMessage>
{
    public async ValueTask HandleAsync(
        PurchasePhotoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
