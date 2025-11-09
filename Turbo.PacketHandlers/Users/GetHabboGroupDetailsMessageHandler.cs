using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class GetHabboGroupDetailsMessageHandler : IMessageHandler<GetHabboGroupDetailsMessage>
{
    public async ValueTask HandleAsync(
        GetHabboGroupDetailsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
