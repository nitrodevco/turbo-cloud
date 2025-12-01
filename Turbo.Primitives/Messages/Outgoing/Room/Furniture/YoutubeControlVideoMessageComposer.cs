using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record YoutubeControlVideoMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
