using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

[GenerateSerializer, Immutable]
public sealed record CameraPublishStatusMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
