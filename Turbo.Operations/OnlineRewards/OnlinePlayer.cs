using System;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Operations.OnlineRewards;

/// <summary>
/// A player logged in to this server, as the online rewards see them: their connection, when it
/// started, and when they last did something (<see cref="ISessionContext.LastActiveUtc"/>).
/// </summary>
internal readonly record struct OnlinePlayer(
    PlayerId PlayerId,
    SessionKey Session,
    DateTime OnlineSinceUtc,
    DateTime LastActiveUtc
);
