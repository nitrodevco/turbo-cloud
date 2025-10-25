using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetSeasonalCalendarDailyOfferMessageHandler
    : IMessageHandler<GetSeasonalCalendarDailyOfferMessage>
{
    public async ValueTask HandleAsync(
        GetSeasonalCalendarDailyOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
