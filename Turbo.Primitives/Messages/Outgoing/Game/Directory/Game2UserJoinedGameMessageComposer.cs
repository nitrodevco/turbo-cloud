using Orleans;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2UserJoinedGameMessageComposer : IComposer
{
    [Id(0)]
    public required GameLobbyPlayerSnapshot Player { get; init; }

    [Id(1)]
    public bool WasTeamSwitched { get; init; }
}
