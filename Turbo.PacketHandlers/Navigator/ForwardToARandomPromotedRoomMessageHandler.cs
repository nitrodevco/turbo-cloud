using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class ForwardToARandomPromotedRoomMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<ForwardToARandomPromotedRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        ForwardToARandomPromotedRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var roomId = await _navigatorService
            .GetRandomPromotedRoomAsync(message.Category, ct)
            .ConfigureAwait(false);

        if (roomId is null)
            return;

        await ctx.SendComposerAsync(new RoomForwardMessageComposer { RoomId = roomId.Value }, ct)
            .ConfigureAwait(false);
    }
}
