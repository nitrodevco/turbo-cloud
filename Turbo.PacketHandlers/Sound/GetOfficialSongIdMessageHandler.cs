using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// The catalog's song disk page names an official song by its code when the product's extra
/// parameter is not a number. Answered with the song's id; an unknown code gets no answer, and
/// the page shows no length.
/// </summary>
public class GetOfficialSongIdMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetOfficialSongIdMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetOfficialSongIdMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.Code))
            return;

        var songId = await _grainFactory
            .GetSongDirectoryGrain()
            .GetSongIdByCodeAsync(message.Code, ct)
            .ConfigureAwait(false);

        if (songId is not { } id)
            return;

        await ctx.SendComposerAsync(
                new OfficialSongIdMessageComposer { Code = message.Code, SongId = id },
                ct
            )
            .ConfigureAwait(false);
    }
}
