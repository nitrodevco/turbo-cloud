using Turbo.Core.Events;

namespace Turbo.Events.Players;

public record PlayerJoinedEvent(long PlayerId) : IEvent;
