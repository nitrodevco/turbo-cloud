using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// The owner opens the room chat filter editor.
/// </summary>
public class GetCustomRoomFilterMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<GetCustomRoomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetCustomRoomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        await ctx.SendRoomFilterAsync(_grainFactory, message.RoomId, ct).ConfigureAwait(false);
    }
}
