using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class BuildersClubQueryFurniCountMessageHandler
    : IMessageHandler<BuildersClubQueryFurniCountMessage>
{
    public async ValueTask HandleAsync(
        BuildersClubQueryFurniCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
