using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Navigator;

public class SetRoomSessionTagsMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<SetRoomSessionTagsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        SetRoomSessionTagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var tags = RoomTags.Normalize(
            [message.Tag1, message.Tag2],
            _navigatorService.MaxTagsPerRoom,
            _navigatorService.MaxTagLength
        );

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetTagsAsync(ctx.AsActionContext(), tags, ct)
            .ConfigureAwait(false);
    }
}
