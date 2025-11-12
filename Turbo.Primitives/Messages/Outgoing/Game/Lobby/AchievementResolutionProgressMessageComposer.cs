using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Game.Lobby;

[GenerateSerializer, Immutable]
public sealed record AchievementResolutionProgressMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
