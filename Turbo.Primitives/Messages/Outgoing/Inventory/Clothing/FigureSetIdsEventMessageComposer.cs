using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;

[GenerateSerializer, Immutable]
public sealed record FigureSetIdsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
