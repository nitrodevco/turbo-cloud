using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Game.Snowwar.Ingame;

[GenerateSerializer, Immutable]
public sealed record Game2FullGameStatusMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
