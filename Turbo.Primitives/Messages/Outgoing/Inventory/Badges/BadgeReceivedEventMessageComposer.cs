using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record BadgeReceivedEventMessageComposer : IComposer
{
    [Id(0)]
    public required string BadgeCode { get; init; }
}
