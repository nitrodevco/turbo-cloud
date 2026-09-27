using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Turbo.Primitives.Networking;

/// <summary>
/// A list the client takes in fragments (the furni, pet and badge tabs, the friend list).
/// There is always at least one fragment, empty for an empty list, because the client waits
/// until it has seen the last one.
/// </summary>
public static class ComposerFragments
{
    /// <param name="perFragment">Entries per fragment; below one counts as one.</param>
    /// <param name="create">Builds one fragment from (total fragments, this index, entries).</param>
    public static List<IComposer> Build<T>(
        IReadOnlyList<T> entries,
        int perFragment,
        Func<int, int, ImmutableArray<T>, IComposer> create
    )
    {
        perFragment = Math.Max(1, perFragment);

        var totalFragments = Math.Max(1, (entries.Count + perFragment - 1) / perFragment);
        var composers = new List<IComposer>(totalFragments);

        for (var fragment = 0; fragment < totalFragments; fragment++)
        {
            var start = fragment * perFragment;
            var length = Math.Max(0, Math.Min(perFragment, entries.Count - start));
            var slice = ImmutableArray.CreateBuilder<T>(length);

            for (var i = 0; i < length; i++)
                slice.Add(entries[start + i]);

            composers.Add(create(totalFragments, fragment, slice.MoveToImmutable()));
        }

        return composers;
    }
}
