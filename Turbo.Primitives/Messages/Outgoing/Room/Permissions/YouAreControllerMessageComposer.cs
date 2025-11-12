using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Permissions;

[GenerateSerializer, Immutable]
public sealed record YouAreControllerMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
