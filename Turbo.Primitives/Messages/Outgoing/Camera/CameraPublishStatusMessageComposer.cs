using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

/// <summary><c>CameraPublishStatusMessageParser</c>: ok, the seconds to wait, and on success the photo's id.</summary>
[GenerateSerializer, Immutable]
public sealed record CameraPublishStatusMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsOk { get; init; }

    [Id(1)]
    public required int SecondsToWait { get; init; }

    [Id(2)]
    public string? ExtraDataId { get; init; }
}
