using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

public class CanCreateRoomMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<CanCreateRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        CanCreateRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var (canCreate, roomLimit) = await _navigatorService
            .CanCreateRoomAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new CanCreateRoomMessageComposer
                {
                    Result = canCreate
                        ? RoomCreationResultType.Allowed
                        : RoomCreationResultType.RoomLimitReached,
                    RoomLimit = roomLimit,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
