using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Repainting a group's furni after its badge or its colours changed.
///
/// The furni holds only the group id and looks the rest up when it attaches, so a room that is
/// not loaded needs no telling — it will read whatever is current the next time it loads. Only
/// rooms already loaded can be holding the old look, and those are exactly the rooms the room
/// directory can name.
///
/// This lives in the handler rather than in the group grain on purpose. The room answers a
/// rights check by asking the group grain, so a group grain that called out to a room while it
/// was still running would have the two waiting on each other. The same rule put the settings
/// push here in the room integration.
/// </summary>
internal static class GuildFurniRefreshExtensions
{
    /// <summary>
    /// Tells every loaded room to repaint this group's furni. Rooms holding none of it answer
    /// after one dictionary scan, which is the usual case; the calls go out together because
    /// each room is its own grain.
    /// </summary>
    public static async Task RefreshGuildFurniEverywhereAsync(
        this IGrainFactory grainFactory,
        GuildId guildId,
        CancellationToken ct
    )
    {
        if (guildId <= 0)
            return;

        var roomIds = await grainFactory
            .GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(ct)
            .ConfigureAwait(false);

        if (roomIds.Length == 0)
            return;

        await Task.WhenAll(
                roomIds.Select(roomId =>
                    grainFactory.GetRoomGrain(roomId).RefreshGuildFurniAsync(guildId, ct)
                )
            )
            .ConfigureAwait(false);
    }
}
