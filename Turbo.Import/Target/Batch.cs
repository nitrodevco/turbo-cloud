using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Import.Target;

/// <summary>Something holding rows not written yet.</summary>
internal interface IFlushable
{
    Task FlushAsync(CancellationToken ct);
}

/// <summary>
/// Rows on their way to one table: written a few thousand at a time, so a table of millions
/// never sits in memory, and counted into the report as they go. A batch whose rows point at
/// another's (a wallet at its player) is made <see cref="After"/> it, and writes it first.
/// </summary>
internal sealed class Batch<T>(
    TableWriter writer,
    ImportReport report,
    ILogger log,
    string table,
    int size = 5_000
) : IFlushable
    where T : class
{
    private readonly List<T> _rows = new(size);
    private readonly List<IFlushable> _parents = [];
    private long _written;

    /// <summary>Writes <paramref name="parents"/> before each write of this one.</summary>
    public Batch<T> After(params IFlushable[] parents)
    {
        _parents.AddRange(parents);

        return this;
    }

    public async Task AddAsync(T row, CancellationToken ct)
    {
        _rows.Add(row);

        if (_rows.Count >= size)
            await FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        if (_rows.Count == 0)
            return;

        foreach (var parent in _parents)
            await parent.FlushAsync(ct).ConfigureAwait(false);

        var count = await writer.InsertAsync(_rows, ct).ConfigureAwait(false);

        _rows.Clear();
        _written += count;
        report.Wrote(table, count);

        if (_written % 100_000 < count)
            log.LogInformation("{Table}: {Written:N0} rows", table, _written);
    }
}
