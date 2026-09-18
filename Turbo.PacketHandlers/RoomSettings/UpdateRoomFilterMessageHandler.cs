using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
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

        var room = _grainFactory.GetRoomGrain(message.RoomId);
        var updated = await room.UpdateRoomFilterAsync(
                ctx.AsActionContext(),
                message.IsAddingWord,
                message.Word,
                ct
            )
            .ConfigureAwait(false);

        if (!updated)
            return;

        var words = await room.GetRoomFilterWordsAsync(ctx.AsActionContext(), ct)
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
