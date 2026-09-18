using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Orleans;

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

        var words = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetRoomFilterWordsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (words is null)
            return;

        await ctx.SendComposerAsync(
                new RoomFilterSettingsMessageComposer { BadWords = [.. words.Value] },
                ct
            )
            .ConfigureAwait(false);
    }
}
