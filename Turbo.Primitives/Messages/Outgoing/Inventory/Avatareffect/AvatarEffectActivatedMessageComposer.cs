using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

/// <summary>A copy of an effect started running. The client ignores <see cref="Duration"/> and counts down from the list it already has.</summary>
[GenerateSerializer, Immutable]
public sealed record AvatarEffectActivatedMessageComposer : IComposer
{
    [Id(0)]
    public required int Type { get; init; }

    [Id(1)]
    public required int Duration { get; init; }

    [Id(2)]
    public required bool IsPermanent { get; init; }
}
