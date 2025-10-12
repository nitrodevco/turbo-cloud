using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

public class AvailabilityStatusMessage : IComposer
{
    public bool IsOpen { get; set; }
    public bool OnShutDown { get; set; }
    public bool IsAuthenticHabbo { get; set; }
}
