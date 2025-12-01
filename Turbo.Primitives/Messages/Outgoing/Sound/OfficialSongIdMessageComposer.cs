using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

[GenerateSerializer, Immutable]
public sealed record OfficialSongIdMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
