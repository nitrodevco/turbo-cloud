using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

[GenerateSerializer, Immutable]
public sealed record CameraPurchaseOKMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
