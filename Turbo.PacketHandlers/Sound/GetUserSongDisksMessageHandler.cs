using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// The playlist editor asks for the song disks in the player's inventory when it opens and
/// whenever the inventory changes. Answered with each disk's id and song; disks in a room or a
/// jukebox are not in the inventory.
/// </summary>
public class GetUserSongDisksMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetUserSongDisksMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetUserSongDisksMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var items = await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .GetAllItemSnapshotsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new UserSongDisksInventoryMessageComposer
                {
                    Disks =
                    [
                        .. items
                            .Where(x => SongDisks.IsSongDisk(x.Definition))
                            .Select(x => new SongDiskSnapshot
                            {
                                DiskId = x.ItemId,
                                SongId = x.Extra,
                            }),
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
