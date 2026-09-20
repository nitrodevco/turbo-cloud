using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;

namespace Docs.Patterns;

// Reference-only sample, and nothing compiles it — check it against the service it mirrors,
// Turbo.Navigator/NavigatorService.cs, before copying it.
//
// A domain service is `public sealed class`, takes a primary constructor, and keeps each
// dependency in a readonly field of the same name with an underscore. Its logger is typed on
// the service interface (ILogger<INavigatorService>), not on the class, so log entries are
// searchable by the contract rather than the implementation. Its tunables come from
// IOptions<TConfig> of its module's config class; it never reads IConfiguration by key.
//
// Unlike a grain, a service is not single-threaded and holds no domain state: it orchestrates
// grains and providers. State belongs to the grain that owns the concept.

/// <summary>
/// Says what this service builds and where its inputs come from, so a reader knows which parts
/// are live and which are cached.
/// </summary>
public sealed class ServicePattern(
    ILogger<ServicePattern> logger,
    IGrainFactory grainFactory,
    IOptions<NavigatorConfig> config
)
{
    private readonly ILogger<ServicePattern> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly NavigatorConfig _config = config.Value;

    /// <summary>
    /// Says what the method does now. When behaviour changes, this line changes with it: a
    /// summary describing the old reply is drift no gate catches.
    /// </summary>
    public async Task<PlayerSummarySnapshot?> TryGetPlayerSummaryAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        // Guard clauses, brace-less, returning the caller's "nothing" rather than throwing.
        if (playerId <= 0)
            return null;

        // Grains are reached through the GrainFactoryExtensions helpers, never through an
        // ad-hoc key literal. Every asynchronous grain method takes the token as its last
        // argument, and every await outside a grain takes ConfigureAwait(false).
        return await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>A limit is read from the config class, never passed in by a caller.</summary>
    public int FavouriteRoomLimit => _config.MaxFavouriteRooms;
}
