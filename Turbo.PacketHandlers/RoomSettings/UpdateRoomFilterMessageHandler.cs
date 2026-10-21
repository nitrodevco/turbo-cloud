using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Adds or removes one word; the client redraws its list from the full set we send back.
/// </summary>
public class UpdateRoomFilterMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<UpdateRoomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        UpdateRoomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        if (
            !await _grainFactory
                .GetRoomGrain(message.RoomId)
                .UpdateRoomFilterAsync(
                    ctx.AsActionContext(),
                    message.IsAddingWord,
                    message.Word,
                    ct
                )
                .ConfigureAwait(false)
        )
            return;

        await ctx.SendRoomFilterAsync(_grainFactory, message.RoomId, ct).ConfigureAwait(false);
    }
}
