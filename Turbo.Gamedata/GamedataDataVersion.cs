using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;

namespace Turbo.Gamedata;

/// <summary>
/// What a view of the hotel's texts, products or figure data against Habbo's is made from, as
/// cheaply as the database can say it: Habbo's newest version and when it was taken in, the hotel's
/// rows and Habbo's last ones (every change the gamedata makes is a change set; others add or
/// remove rows). Two equal versions see the same rows, so what was worked out from one can be kept
/// for the other.
/// </summary>
internal sealed record GamedataDataVersion(
    int? Latest,
    DateTime? LatestImported,
    int? ChangeSet,
    int Ours,
    int? MaxOurs,
    int Bases,
    int? MaxBases
)
{
    /// <param name="ours">The ids of the hotel's rows.</param>
    /// <param name="bases">The ids of Habbo's rows as last taken in.</param>
    public static async Task<GamedataDataVersion> ReadAsync(
        TurboDbContext dbCtx,
        int? latest,
        DateTime? latestImported,
        IQueryable<int> ours,
        IQueryable<int> bases,
        CancellationToken ct
    ) =>
        new(
            latest,
            latestImported,
            await dbCtx
                .GamedataChangeSets.AsNoTracking()
                .MaxAsync(x => (int?)x.Id, ct)
                .ConfigureAwait(false),
            await ours.CountAsync(ct).ConfigureAwait(false),
            await ours.MaxAsync(x => (int?)x, ct).ConfigureAwait(false),
            await bases.CountAsync(ct).ConfigureAwait(false),
            await bases.MaxAsync(x => (int?)x, ct).ConfigureAwait(false)
        );
}
