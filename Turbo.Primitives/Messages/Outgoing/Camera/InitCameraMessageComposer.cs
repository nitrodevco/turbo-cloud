using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

/// <summary><c>InitCameraMessageParser</c>: the poster's credit and ducket prices, then the publish price.</summary>
[GenerateSerializer, Immutable]
public sealed record InitCameraMessageComposer : IComposer
{
    [Id(0)]
    public required int CreditPrice { get; init; }

    [Id(1)]
    public required int DucketPrice { get; init; }

    [Id(2)]
    public required int PublishDucketPrice { get; init; }
}
