using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Achievements;

public interface IAchievementPackRegistry
{
    ImmutableArray<IAchievementPack> Packs { get; }

    /// <summary>
    /// Registers a pack; disposing the result removes it. A pack with a repeated key, an invalid
    /// range, a range another pack owns, or a definition outside its own ranges is rejected. A
    /// registered pack is installed the next time the catalog is reloaded.
    /// </summary>
    IDisposable Register(IAchievementPack pack);
}
