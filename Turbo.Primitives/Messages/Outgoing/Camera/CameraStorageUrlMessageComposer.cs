using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

/// <summary>
/// <c>CameraStorageUrlMessageParser</c>: where the rendered photo is, after the client's
/// <c>stories.image_url_base</c>; empty when the daily render limit is hit.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CameraStorageUrlMessageComposer : IComposer
{
    [Id(0)]
    public required string Url { get; init; }
}
