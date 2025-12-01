using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record MaintenanceStatusMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
