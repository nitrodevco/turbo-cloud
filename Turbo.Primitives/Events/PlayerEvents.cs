using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Events;

public record PlayerJoinedEvent(long PlayerId) : IEvent;
