using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Turbo.Admin.Api.Contracts;

namespace Turbo.Admin.Api;

/// <summary>
/// The columns a list can be sorted by: each a key the panel sends (<c>sort</c>), the way it reads
/// when none is asked for (<c>dir</c>), and what it orders by, most significant first, ending in
/// something unique so a row never moves between pages. The first column added is the list's
/// default. A column or way it does not know reads as the default, so the address can't order a
/// list by anything it was not built to, and a big log only by what its indexes make cheap.
/// </summary>
public sealed class ListSorts<T>
{
    private readonly List<(string Key, string Dir, LambdaExpression[] By)> _columns = [];

    public ListSorts<T> Add(string key, string dir, params Expression<Func<T, object?>>[] by)
    {
        _columns.Add((key, dir, [.. by.Select(Typed)]));

        return this;
    }

    /// <summary>The order asked for, or the default for what was not.</summary>
    public ListOrder Resolve(string? sort, string? dir)
    {
        var column = _columns.Find(x => x.Key == sort);

        if (column.Key is null)
            return new ListOrder(_columns[0].Key, _columns[0].Dir);

        return new ListOrder(column.Key, dir is ListOrder.ASC or ListOrder.DESC ? dir : column.Dir);
    }

    /// <summary>The rows in <paramref name="order"/>, every key of its column the same way.</summary>
    public IOrderedQueryable<T> Apply(IQueryable<T> rows, ListOrder order)
    {
        var column = _columns.Find(x => x.Key == order.Sort);

        if (column.Key is null)
            column = _columns[0];

        var descending = order.Dir == ListOrder.DESC ? "Descending" : string.Empty;
        var expression = rows.Expression;

        for (var i = 0; i < column.By.Length; i++)
            expression = Expression.Call(
                typeof(Queryable),
                (i == 0 ? "OrderBy" : "ThenBy") + descending,
                [typeof(T), column.By[i].ReturnType],
                expression,
                Expression.Quote(column.By[i])
            );

        return (IOrderedQueryable<T>)rows.Provider.CreateQuery<T>(expression);
    }

    /// <summary>
    /// The key as its own type rather than boxed to <see cref="object"/>, which a database can't
    /// order by.
    /// </summary>
    private static LambdaExpression Typed(Expression<Func<T, object?>> by) =>
        Expression.Lambda(
            by.Body is UnaryExpression { NodeType: ExpressionType.Convert } boxed
                ? boxed.Operand
                : by.Body,
            by.Parameters
        );
}
