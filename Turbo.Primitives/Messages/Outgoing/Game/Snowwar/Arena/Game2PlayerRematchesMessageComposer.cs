using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Game.Snowwar.Arena;

[GenerateSerializer, Immutable]
public sealed record Game2PlayerRematchesMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
