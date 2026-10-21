using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Gamedata.Habbo;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// What a view of the furniture definitions against Habbo's releases is made from, as cheaply as
/// the database can say it: the definitions (every change the gamedata makes is a change set;
/// others add or remove rows), the asset files read, and the releases found and taken in. Two
/// equal versions see the same rows, so what was worked out from one can be kept for the other.
/// </summary>
internal sealed record FurnitureDataVersion(
    int Definitions,
    int? MaxDefinitionId,
    int? ChangeSet,
    int AssetFiles,
    int? Release,
    DateTime? ReleaseImported
)
{
    public static async Task<FurnitureDataVersion> ReadAsync(
        TurboDbContext dbCtx,
        string domain,
        CancellationToken ct
    )
    {
        var definitions = dbCtx.FurnitureDefinitions.AsNoTracking();
        var release = await dbCtx
            .HabboReleases.AsNoTracking()
            .Latest(domain)
            .Select(x => new { x.Id, x.ImportedAt })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new FurnitureDataVersion(
            await definitions.CountAsync(ct).ConfigureAwait(false),
            await definitions.MaxAsync(x => (int?)x.Id, ct).ConfigureAwait(false),
            await dbCtx
                .GamedataChangeSets.AsNoTracking()
                .MaxAsync(x => (int?)x.Id, ct)
                .ConfigureAwait(false),
            await dbCtx.HabboFurnitureAssets.AsNoTracking().CountAsync(ct).ConfigureAwait(false),
            release?.Id,
            release?.ImportedAt
        );
    }
}
