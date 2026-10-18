using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Turbo.Database.Entities.Movements;

namespace Turbo.Operations.Movements;

/// <summary>
/// The outbox ids one consumer's reading stepped over, as ranges with when they were noticed. An
/// id is skipped when its save rolled back (it never holds a row) or had not committed yet (its
/// row appears later, below the cursor); the second kind is looked for until the re-read window
/// passes. At most <c>capacity</c> ranges are kept, the oldest dropped first.
/// </summary>
internal sealed class SkippedOutboxIds(int capacity)
{
    private readonly List<(long First, long Last, DateTime NoticedAtUtc)> _ranges = [];

    public bool IsEmpty => _ranges.Count == 0;

    public void Add(long first, long last, DateTime noticedAtUtc)
    {
        if (first > last)
            return;

        if (_ranges.Count >= capacity)
            _ranges.RemoveAt(0);

        _ranges.Add((first, last, noticedAtUtc));
    }

    /// <summary>Stops looking for ids noticed before <paramref name="cutoffUtc"/>.</summary>
    public void Expire(DateTime cutoffUtc) => _ranges.RemoveAll(x => x.NoticedAtUtc < cutoffUtc);

    /// <summary>Stops looking for <paramref name="id"/>: its row was found.</summary>
    public void Found(long id)
    {
        var index = _ranges.FindIndex(x => x.First <= id && id <= x.Last);

        if (index < 0)
            return;

        var (first, last, noticed) = _ranges[index];
        _ranges.RemoveAt(index);

        if (id < last)
            _ranges.Insert(index, (id + 1, last, noticed));

        if (first < id)
            _ranges.Insert(index, (first, id - 1, noticed));
    }

    /// <summary>A row whose id is in any range: one query for all of them.</summary>
    public Expression<Func<AssetMovementOutboxEntity, bool>> Predicate()
    {
        var row = Expression.Parameter(typeof(AssetMovementOutboxEntity), "x");
        var id = Expression.Property(row, nameof(AssetMovementOutboxEntity.Id));

        var any = _ranges
            .Select(x =>
                (Expression)
                    Expression.AndAlso(
                        Expression.GreaterThanOrEqual(id, Expression.Constant(x.First)),
                        Expression.LessThanOrEqual(id, Expression.Constant(x.Last))
                    )
            )
            .Aggregate(Expression.OrElse);

        return Expression.Lambda<Func<AssetMovementOutboxEntity, bool>>(any, row);
    }
}
