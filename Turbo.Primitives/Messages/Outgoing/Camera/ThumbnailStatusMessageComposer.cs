using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

[GenerateSerializer, Immutable]
public sealed record ThumbnailStatusMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
