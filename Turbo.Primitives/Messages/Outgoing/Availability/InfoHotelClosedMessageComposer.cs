using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record InfoHotelClosedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
