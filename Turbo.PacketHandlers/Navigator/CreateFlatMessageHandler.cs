using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class CreateFlatMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<CreateFlatMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        CreateFlatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var roomId = await _navigatorService
            .CreateRoomAsync(
                ctx.PlayerId,
                message.FlatName,
                message.FlatDescription,
                message.FlatModelName,
                message.CategoryID,
                message.MaxPlayers,
                message.TradeSetting,
                ct
            )
            .ConfigureAwait(false);

        if (roomId is null)
            return;

        await ctx.SendComposerAsync(
                new FlatCreatedMessageComposer
                {
                    RoomId = roomId.Value,
                    Name = message.FlatName.Trim(),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
