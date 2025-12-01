using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record AvailabilityStatusMessageComposer : IComposer
{
    [Id(0)]
    public bool IsOpen { get; init; }

    [Id(1)]
    public bool OnShutDown { get; init; }

    [Id(2)]
    public bool IsAuthenticHabbo { get; init; }
}
