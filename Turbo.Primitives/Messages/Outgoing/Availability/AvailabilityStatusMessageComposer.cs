using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

public record AvailabilityStatusMessageComposer : IComposer
{
    public bool IsOpen { get; init; }
    public bool OnShutDown { get; init; }
    public bool IsAuthenticHabbo { get; init; }
}
