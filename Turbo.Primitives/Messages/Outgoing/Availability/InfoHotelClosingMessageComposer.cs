using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record InfoHotelClosingMessageComposer : IComposer
{
    [Id(0)]
    public required int MinutesUntilClosing { get; init; }
}
