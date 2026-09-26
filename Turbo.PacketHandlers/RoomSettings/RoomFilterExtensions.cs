using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.RoomSettings;

internal static class RoomFilterExtensions
{
    /// <summary>
    /// Sends the room's whole word filter, or nothing when the sender may not see it. Asked for
    /// when the editor opens, and sent again after each change, because the client redraws its
    /// list from the full set rather than applying the one word.
    /// </summary>
    public static async Task SendRoomFilterAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var words = await grainFactory
            .GetRoomGrain(roomId)
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
