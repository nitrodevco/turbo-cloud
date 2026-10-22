using Orleans;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Game.Directory;

[GenerateSerializer, Immutable]
public sealed record Game2GameStartedMessageComposer : IComposer
{
    [Id(0)]
    public required GameLobbySnapshot Lobby { get; init; }
}
