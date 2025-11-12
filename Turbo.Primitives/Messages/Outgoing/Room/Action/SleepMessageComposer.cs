using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Action;

[GenerateSerializer, Immutable]
public sealed record SleepMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
