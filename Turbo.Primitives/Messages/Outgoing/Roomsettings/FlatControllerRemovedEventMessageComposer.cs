using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record FlatControllerRemovedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
