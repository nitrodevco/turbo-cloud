using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

/// <summary>The player was given an effect. The client adds one copy to an existing type, or lists a new inactive one.</summary>
[GenerateSerializer, Immutable]
public sealed record AvatarEffectAddedMessageComposer : IComposer
{
    [Id(0)]
    public required int Type { get; init; }

    [Id(1)]
    public required int SubType { get; init; }

    [Id(2)]
    public required int Duration { get; init; }

    [Id(3)]
    public required bool IsPermanent { get; init; }
}
