using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Adds or removes one word; the client redraws its list from the full set we send back.
/// </summary>
public class UpdateRoomFilterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateRoomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateRoomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
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
