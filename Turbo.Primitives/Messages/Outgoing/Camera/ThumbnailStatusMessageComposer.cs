using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

/// <summary><c>ThumbnailStatusMessageParser</c>: ok, and whether the daily render limit is hit.</summary>
[GenerateSerializer, Immutable]
public sealed record ThumbnailStatusMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsOk { get; init; }

    [Id(1)]
    public required bool IsRenderLimitHit { get; init; }
}
