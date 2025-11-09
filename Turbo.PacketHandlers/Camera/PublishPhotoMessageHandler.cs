using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;

namespace Turbo.PacketHandlers.Camera;

public class PublishPhotoMessageHandler : IMessageHandler<PublishPhotoMessage>
{
    public async ValueTask HandleAsync(
        PublishPhotoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
