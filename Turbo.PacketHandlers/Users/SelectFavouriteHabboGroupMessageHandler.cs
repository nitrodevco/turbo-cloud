using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class SelectFavouriteHabboGroupMessageHandler
    : IMessageHandler<SelectFavouriteHabboGroupMessage>
{
    public async ValueTask HandleAsync(
        SelectFavouriteHabboGroupMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
