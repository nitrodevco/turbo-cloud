using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

[GenerateSerializer, Immutable]
public sealed record PetBreedingResultEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
