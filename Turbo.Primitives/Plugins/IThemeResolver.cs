using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>Resolves audience rules without owning client display state.</summary>
public interface IThemeResolver
{
    /// <summary>
    /// Resolves the default and every supplied player in a batch. Values are the preview requests
    /// accepted during session initialization, or null. Recheck current permissions and choices;
    /// null theme keys mean the hotel's combined look. Return one entry for every supplied player.
    /// </summary>
    public Task<ThemeResolution> ResolveAsync(
        IReadOnlyDictionary<PlayerId, string?> requests,
        CancellationToken ct
    );
}
