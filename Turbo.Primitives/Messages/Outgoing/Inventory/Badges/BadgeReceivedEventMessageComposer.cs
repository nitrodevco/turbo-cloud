using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record BadgeReceivedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
