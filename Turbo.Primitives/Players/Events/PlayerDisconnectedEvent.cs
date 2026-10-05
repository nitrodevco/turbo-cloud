using Turbo.Primitives.Events;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// Raised once when a logged-in session is unbound from its player, however the connection ended.
/// A session that never logged in raises nothing.
/// </summary>
public sealed record PlayerDisconnectedEvent(PlayerId PlayerId, SessionKey SessionKey) : IEvent;
