using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record InfoHotelClosingMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
