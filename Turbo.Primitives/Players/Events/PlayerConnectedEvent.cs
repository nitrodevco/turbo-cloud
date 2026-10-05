using Turbo.Primitives.Events;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Players.Events;

/// <summary>Raised once when a session is bound to a player, as the login begins.</summary>
public sealed record PlayerConnectedEvent(PlayerId PlayerId, SessionKey SessionKey) : IEvent;
