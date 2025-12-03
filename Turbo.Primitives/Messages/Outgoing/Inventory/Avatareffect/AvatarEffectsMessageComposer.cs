using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

[GenerateSerializer, Immutable]
public sealed record AvatarEffectsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<AvatarEffectSnapshot> Effects { get; init; }
}
