using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

/// <summary>The running copy of an effect ran out. The client drops the type, or returns it to inactive when copies remain.</summary>
[GenerateSerializer, Immutable]
public sealed record AvatarEffectExpiredMessageComposer : IComposer
{
    [Id(0)]
    public required int Type { get; init; }
}
