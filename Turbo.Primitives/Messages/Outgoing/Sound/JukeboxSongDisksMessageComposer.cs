using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

[GenerateSerializer, Immutable]
public sealed record JukeboxSongDisksMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
