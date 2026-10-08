using System;
using System.Collections.Generic;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Furniture.Grains;

internal sealed class SongDirectoryLiveState
{
    public Dictionary<int, SongSnapshot> SongsById { get; } = [];

    /// <summary>Official songs' catalog codes, which the client may send in any case.</summary>
    public Dictionary<string, int> IdsByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
}
