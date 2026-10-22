using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Games;

/// <summary>
/// The games the game centre offers, and the reward handlers that hear their results. Plugins
/// are registered here by the assembly scan; copy-on-write, so a reader never sees a half-changed
/// list.
/// </summary>
public interface IGameRegistry
{
    public IReadOnlyList<IRoomGame> Games { get; }

    public IReadOnlyList<IGameRewardHandler> RewardHandlers { get; }

    /// <summary>
    /// Adds games and reward handlers; disposing the result removes them. A game whose type id or
    /// key is already registered is refused with an <see cref="ArgumentException"/>.
    /// </summary>
    public IDisposable Register(
        IEnumerable<IRoomGame> games,
        IEnumerable<IGameRewardHandler> rewardHandlers
    );

    public IRoomGame? Find(int gameTypeId);

    public IRoomGame? Find(string key);
}
