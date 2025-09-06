using Turbo.Primitives;

namespace Turbo.Contracts.Events;

public record PlayerJoinedEvent(long PlayerId) : IEvent;
