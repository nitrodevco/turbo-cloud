using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotForceOpenContextMenuMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
