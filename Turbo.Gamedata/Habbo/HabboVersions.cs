using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Gamedata;

namespace Turbo.Gamedata.Habbo;

/// <summary>Finding the versions of Habbo's files a check kept (<see cref="IHabboVersionEntity"/>).</summary>
internal static class HabboVersions
{
    /// <summary>The domain's versions, the newest first.</summary>
    public static IQueryable<T> Latest<T>(this IQueryable<T> versions, string domain)
        where T : TurboEntity, IHabboVersionEntity =>
        versions.Where(x => x.Domain == domain).OrderByDescending(x => x.Id);

    /// <summary>The version <paramref name="id"/>, or the domain's newest when none is named.</summary>
    public static Task<T?> FindOrLatestAsync<T>(
        this IQueryable<T> versions,
        int? id,
        string domain,
        CancellationToken ct
    )
        where T : TurboEntity, IHabboVersionEntity =>
        id is { } wanted
            ? versions.FirstOrDefaultAsync(x => x.Id == wanted, ct)
            : versions.Latest(domain).FirstOrDefaultAsync(ct);
}
