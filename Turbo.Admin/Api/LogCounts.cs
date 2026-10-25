using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Admin.Api;

/// <summary>
/// How a log that grows with everything players do (the chat log, private messages, the currency
/// ledger, room visits) is counted for its pages: up to <see cref="LIMIT"/> rows and no further, so
/// showing one page never reads the whole log to say how many there are. Past the limit the panel
/// says "10,000+" and its pages stop there; turning the order round (oldest first) or narrowing the
/// filter reaches the rest.
/// </summary>
public static class LogCounts
{
    public const int LIMIT = 10_000;

    /// <summary>
    /// The rows counted, at most <see cref="LIMIT"/>, and whether there are more. The rows come in
    /// the page's order, which a log keeps to its indexes: a limit needs one to be the same rows
    /// each time.
    /// </summary>
    public static async Task<(int Total, bool Capped)> CountAsync<T>(
        IOrderedQueryable<T> rows,
        CancellationToken ct
    )
    {
        var counted = await rows.Take(LIMIT + 1).CountAsync(ct).ConfigureAwait(false);

        return (Math.Min(counted, LIMIT), counted > LIMIT);
    }

    /// <summary>The page asked for, from the first to the last that <paramref name="total"/> fills.</summary>
    public static int Page(int page, int total, int pageSize) =>
        Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
}
