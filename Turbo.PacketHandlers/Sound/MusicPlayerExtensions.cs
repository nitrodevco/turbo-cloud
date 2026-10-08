using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// Handing a jukebox packet to the room the sender is standing in. None of them names an item:
/// the client keeps one music player per room, and the room finds its jukebox or sound machine,
/// which decides whether this player may and answers.
/// </summary>
internal static class MusicPlayerExtensions
{
    /// <summary>
    /// Sends the interaction to the sender's current room, or nothing when there is no player or
    /// no room. Returns whether the room carried it out.
    /// </summary>
    public static async Task<bool> InteractWithMusicPlayerAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return false;

        return await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithMusicPlayerAsync(ctx.AsActionContext(), interaction, ct)
            .ConfigureAwait(false);
    }
}
