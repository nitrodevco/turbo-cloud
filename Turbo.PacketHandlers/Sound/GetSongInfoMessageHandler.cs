using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// The client asks once a second for the songs it has met (on a disk, in a playlist, on a
/// catalog page) and knows nothing about yet. Answered with those the hotel has, track included;
/// unknown ids are left out, and nothing is sent when none is known.
/// </summary>
public class GetSongInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetSongInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSongInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.SongIds.Count == 0)
            return;

        var songs = await _grainFactory
            .GetSongDirectoryGrain()
            .GetSongsAsync([.. message.SongIds.Where(id => id > 0)], ct)
            .ConfigureAwait(false);

        if (songs.IsDefaultOrEmpty)
            return;

        await ctx.SendComposerAsync(new TraxSongInfoMessageComposer { Songs = songs }, ct)
            .ConfigureAwait(false);
    }
}
