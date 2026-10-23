using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Turbo.Primitives.Games;

namespace Turbo.Rooms.Games;

/// <summary>
/// Copy-on-write, as <see cref="RoomEventListenerRegistry"/>: a registration builds new arrays,
/// so a lobby being launched keeps the games it read.
/// </summary>
public sealed class GameRegistry : IGameRegistry
{
    private readonly Lock _gate = new();
    private volatile IRoomGame[] _games = [];
    private volatile IGameRewardHandler[] _rewardHandlers = [];
    private volatile IGameAccessPolicy[] _accessPolicies = [];

    public IReadOnlyList<IRoomGame> Games => _games;

    public IReadOnlyList<IGameRewardHandler> RewardHandlers => _rewardHandlers;

    /// <summary>The access policy a plugin registered last, or null for the server's own.</summary>
    public IGameAccessPolicy? AccessPolicy => _accessPolicies.LastOrDefault();

    public IRoomGame? Find(int gameTypeId) =>
        _games.FirstOrDefault(game => game.Definition.GameTypeId == gameTypeId);

    public IRoomGame? Find(string key) =>
        _games.FirstOrDefault(game =>
            string.Equals(game.Definition.Key, key, StringComparison.OrdinalIgnoreCase)
        );

    public IDisposable Register(
        IEnumerable<IRoomGame> games,
        IEnumerable<IGameRewardHandler> rewardHandlers
    ) => Register(games, rewardHandlers, []);

    public IDisposable Register(
        IEnumerable<IRoomGame> games,
        IEnumerable<IGameRewardHandler> rewardHandlers,
        IEnumerable<IGameAccessPolicy> accessPolicies
    )
    {
        var gameBatch = games.ToArray();
        var handlerBatch = rewardHandlers.ToArray();
        var policyBatch = accessPolicies.ToArray();

        lock (_gate)
        {
            foreach (var game in gameBatch)
            {
                var definition = game.Definition;
                var taken = _games
                    .Concat(gameBatch.Where(other => other != game))
                    .Any(other =>
                        other.Definition.GameTypeId == definition.GameTypeId
                        || string.Equals(
                            other.Definition.Key,
                            definition.Key,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                if (taken)
                    throw new ArgumentException(
                        $"A game with type id {definition.GameTypeId} or key {definition.Key} is already registered."
                    );
            }

            _games = [.. _games, .. gameBatch];
            _rewardHandlers = [.. _rewardHandlers, .. handlerBatch];
            _accessPolicies = [.. _accessPolicies, .. policyBatch];
        }

        return new Registration(this, gameBatch, handlerBatch, policyBatch);
    }

    private void Remove(
        IRoomGame[] games,
        IGameRewardHandler[] handlers,
        IGameAccessPolicy[] policies
    )
    {
        lock (_gate)
        {
            _games = [.. _games.Where(x => !games.Contains(x))];
            _rewardHandlers = [.. _rewardHandlers.Where(x => !handlers.Contains(x))];
            _accessPolicies = [.. _accessPolicies.Where(x => !policies.Contains(x))];
        }
    }

    private sealed class Registration(
        GameRegistry owner,
        IRoomGame[] games,
        IGameRewardHandler[] handlers,
        IGameAccessPolicy[] policies
    ) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(games, handlers, policies);
        }
    }
}
