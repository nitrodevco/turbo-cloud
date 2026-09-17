using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class GetOfficialRoomsMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<GetOfficialRoomsMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetOfficialRoomsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var rooms = await _navigatorService.GetOfficialRoomsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new OfficialRoomsMessageComposer { Rooms = [.. rooms] }, ct)
            .ConfigureAwait(false);
    }
}
