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

    public IReadOnlyList<IRoomGame> Games => _games;

    public IReadOnlyList<IGameRewardHandler> RewardHandlers => _rewardHandlers;

    public IRoomGame? Find(int gameTypeId) =>
        _games.FirstOrDefault(game => game.Definition.GameTypeId == gameTypeId);

    public IRoomGame? Find(string key) =>
        _games.FirstOrDefault(game =>
            string.Equals(game.Definition.Key, key, StringComparison.OrdinalIgnoreCase)
        );

    public IDisposable Register(
        IEnumerable<IRoomGame> games,
        IEnumerable<IGameRewardHandler> rewardHandlers
    )
    {
        var gameBatch = games.ToArray();
        var handlerBatch = rewardHandlers.ToArray();

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
        }

        return new Registration(this, gameBatch, handlerBatch);
    }

    private void Remove(IRoomGame[] games, IGameRewardHandler[] handlers)
    {
        lock (_gate)
        {
            _games = [.. _games.Where(x => !games.Contains(x))];
            _rewardHandlers = [.. _rewardHandlers.Where(x => !handlers.Contains(x))];
        }
    }

    private sealed class Registration(
        GameRegistry owner,
        IRoomGame[] games,
        IGameRewardHandler[] handlers
    ) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(games, handlers);
        }
    }
}
