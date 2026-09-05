using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Advertisement;

[GenerateSerializer, Immutable]
public sealed record InterstitialMessageComposer : IComposer
{
    [Id(0)]
    public required bool CanShowInterstitial { get; init; }
}
