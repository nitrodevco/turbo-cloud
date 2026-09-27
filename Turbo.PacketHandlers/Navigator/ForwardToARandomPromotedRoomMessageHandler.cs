using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class ForwardToARandomPromotedRoomMessageHandler(
    INavigatorService navigatorService,
    IGrainFactory grainFactory
) : IMessageHandler<ForwardToARandomPromotedRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly IGrainFactory _grainFactory = grainFactory;

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

        await _grainFactory
            .ForwardPlayerToRoomAsync(ctx.PlayerId, roomId.Value, ct)
            .ConfigureAwait(false);
    }
}
