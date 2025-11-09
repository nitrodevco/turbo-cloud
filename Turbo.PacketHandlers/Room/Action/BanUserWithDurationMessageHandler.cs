using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;

namespace Turbo.PacketHandlers.Room.Action;

public class BanUserWithDurationMessageHandler : IMessageHandler<BanUserWithDurationMessage>
{
    public async ValueTask HandleAsync(
        BanUserWithDurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
