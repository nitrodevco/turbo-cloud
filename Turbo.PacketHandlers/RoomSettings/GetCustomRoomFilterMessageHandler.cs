using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// The owner opens the room chat filter editor.
/// </summary>
public class GetCustomRoomFilterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCustomRoomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetCustomRoomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        await ctx.SendRoomFilterAsync(_grainFactory, message.RoomId, ct).ConfigureAwait(false);
    }
}
