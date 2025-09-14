using System;
using System.Collections.Generic;

namespace Turbo.Pipeline;

public static class TypeUtil
{
    public static bool TryGetGenericAncestor(Type candidate, Type openGeneric, out Type closed)
    {
        if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openGeneric)
        {
            closed = candidate;
            return true;
        }

        foreach (var i in candidate.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric)
            {
                closed = i;
                return true;
            }

        for (var bt = candidate.BaseType; bt is not null; bt = bt.BaseType)
            if (bt.IsGenericType && bt.GetGenericTypeDefinition() == openGeneric)
            {
                closed = bt;
                return true;
            }

        closed = null!;
        return false;
    }

    public static void InsertOrdered<T>(List<T> list, T item, Comparison<T> cmp)
    {
        int idx = list.BinarySearch(item, Comparer<T>.Create(cmp));
        if (idx < 0)
            idx = ~idx;
        list.Insert(idx, item);
    }
}
