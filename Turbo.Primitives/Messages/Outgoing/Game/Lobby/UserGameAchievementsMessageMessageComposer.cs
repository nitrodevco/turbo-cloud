using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Game.Lobby;

[GenerateSerializer, Immutable]
public sealed record UserGameAchievementsMessageMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
