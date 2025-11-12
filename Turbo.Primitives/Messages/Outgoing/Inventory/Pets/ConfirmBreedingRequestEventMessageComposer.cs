using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record ConfirmBreedingRequestEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
