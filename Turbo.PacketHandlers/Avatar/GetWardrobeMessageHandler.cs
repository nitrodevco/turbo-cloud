using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;

namespace Turbo.PacketHandlers.Avatar;

public class GetWardrobeMessageHandler : IMessageHandler<GetWardrobeMessage>
{
    public async ValueTask HandleAsync(
        GetWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
