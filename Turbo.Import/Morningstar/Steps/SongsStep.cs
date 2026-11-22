using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Sound;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The hotel's trax songs (<c>soundtracks</c>), which a music disc names by id. A song the target
/// has by code is that one; another is added. The furniture step writes each disc with Turbo's id.
/// </summary>
internal sealed class SongsStep : IImportStep
{
    public const string TABLE = "songs";

    public string Name => "songs";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var byCode = await ctx
            .Db.Songs.AsNoTracking()
            .Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, StringComparer.OrdinalIgnoreCase, ct)
            .ConfigureAwait(false);

        await foreach (
            var row in ctx.Source.ReadAsync("soundtracks", "id", ct).ConfigureAwait(false)
        )
        {
            var id = row.Int("id");
            var code = Cut(row.Text("code").Trim(), SongEntity.CODE_MAX_LENGTH);

            if (code.Length > 0 && byCode.TryGetValue(code, out var existing))
            {
                ctx.Songs[id] = existing;

                continue;
            }

            var track = row.Text("track");

            if (track.Length == 0)
            {
                ctx.Report.Note("song left out: it has no track", $"soundtrack {id}");

                continue;
            }

            ctx.Songs[id] = await ctx.AddSetupAsync(
                    new SongEntity
                    {
                        Code = code.Length == 0 ? null : code,
                        Name = Cut(row.Text("name", $"Song {id}"), SongEntity.NAME_MAX_LENGTH),
                        Author = Cut(row.Text("author"), SongEntity.AUTHOR_MAX_LENGTH),
                        Track = track,
                        LengthSeconds = Math.Max(0, row.Int("length")),
                    },
                    TABLE,
                    ct
                )
                .ConfigureAwait(false);

            if (code.Length > 0)
                byCode[code] = ctx.Songs[id];
        }
    }

    private static string Cut(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
