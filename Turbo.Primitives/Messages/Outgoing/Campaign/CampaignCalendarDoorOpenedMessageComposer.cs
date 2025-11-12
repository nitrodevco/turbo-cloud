using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Campaign;

[GenerateSerializer, Immutable]
public sealed record CampaignCalendarDoorOpenedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
