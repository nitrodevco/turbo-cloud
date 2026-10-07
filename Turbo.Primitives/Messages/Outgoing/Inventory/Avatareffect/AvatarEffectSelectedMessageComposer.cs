using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

/// <summary>The effect the player now wears. Only the avatar editor reads it; the room learns from the avatar effect message.</summary>
[GenerateSerializer, Immutable]
public sealed record AvatarEffectSelectedMessageComposer : IComposer
{
    [Id(0)]
    public required int Type { get; init; }
}
