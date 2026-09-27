using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Players.Grains;

/// <summary>
/// Player names and ids for the whole hotel, one grain. It is a read-through cache over the
/// players table: nothing here is written back, so there is nothing to flush on deactivation.
/// The cache is bounded (<see cref="PlayerConfig.DirectoryMaxCachedPlayers"/>).
///
/// Every read is interleaved (see <see cref="IPlayerDirectoryGrain"/>): a miss queries the
/// database without holding up the lookups behind it. That is safe because no method holds
/// anything across its await: each touches the cache only in synchronous stretches, and a read
/// fills the cache only for a player it does not hold yet, so a rename that landed while the
/// query ran is never overwritten by the older name the query saw.
/// </summary>
[KeepAlive]
internal sealed class PlayerDirectoryGrain : Grain, IPlayerDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly ILogger<IPlayerDirectoryGrain> _logger;

    private readonly PlayerDirectoryLiveState _state;

    public PlayerDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        ILogger<IPlayerDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _logger = logger;

        _state = new() { Names = new(playerConfig.Value.DirectoryMaxCachedPlayers) };
    }

    public async Task<string> GetPlayerNameAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_state.Names.TryGetName(playerId, out var name))
            return name;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var dbName = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == (int)playerId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(dbName))
        {
            // Asking for a player who is not there is a caller bug, not an outage.
            _logger.LogWarning("No name found for player {PlayerId}", playerId);

            return string.Empty;
        }

        FillIfAbsent(playerId, dbName);

        return dbName;
    }

    public async Task<ImmutableDictionary<PlayerId, string>> GetPlayerNamesAsync(
        List<PlayerId> playerIds,
        CancellationToken ct
    )
    {
        var names = new Dictionary<PlayerId, string>();

        // One id or many, the same rule: an unknown player is left out. A special case for a
        // single id used to answer "" for them instead.
        var ids = playerIds.Distinct().ToList();
        var notFound = new List<int>();

        foreach (var playerId in ids)
        {
            if (_state.Names.TryGetName(playerId, out var name))
            {
                names.TryAdd(playerId, name);
            }
            else
            {
                notFound.Add(playerId.Value);
            }
        }

        if (notFound.Count > 0)
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var players = await dbCtx
                .Players.AsNoTracking()
                .Where(x => notFound.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            foreach (var player in players)
            {
                FillIfAbsent(player.Key, player.Value);

                names.TryAdd(player.Key, player.Value);
            }
        }

        return names.ToImmutableDictionary();
    }

    public Task SetPlayerNameAsync(PlayerId playerId, string name, CancellationToken ct)
    {
        _state.Names.Set(playerId, name);

        return Task.CompletedTask;
    }

    public async Task<PlayerId?> GetPlayerIdAsync(string name, CancellationToken ct)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (_state.Names.TryGetId(name, out var playerId))
            return playerId;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // Plain equality, so the unique index on the name answers it. The column's collation
        // is case-insensitive (utf8mb4 *_ci), which is what the lookup needs; lowering both
        // sides compared the same way but read every row.
        var player = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Name == name)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);

        if (player is null)
            return null;

        playerId = PlayerId.Parse(player.Id);

        FillIfAbsent(playerId, player.Name);

        return playerId;
    }

    /// <summary>
    /// Caches what a read found, unless the player was cached while the query ran: that entry
    /// came from <see cref="SetPlayerNameAsync"/> or a later read and is at least as new.
    /// </summary>
    private void FillIfAbsent(PlayerId playerId, string name)
    {
        if (!_state.Names.Contains(playerId))
            _state.Names.Set(playerId, name);
    }
}
