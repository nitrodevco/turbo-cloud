using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>
/// One song a sound machine's playlist gained, appended by the client to the list it plays.
/// Nothing sends it yet: this client cannot load songs into a sound machine.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PlayListSongAddedMessageComposer : IComposer
{
    [Id(0)]
    public required SongSnapshot Song { get; init; }
}
