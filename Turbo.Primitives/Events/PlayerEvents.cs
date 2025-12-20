using Turbo.Primitives.Players;

namespace Turbo.Primitives.Events;

public record PlayerJoinedEvent(PlayerId PlayerId) : IEvent;
