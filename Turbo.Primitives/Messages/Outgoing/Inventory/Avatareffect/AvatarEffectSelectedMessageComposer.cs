using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;

[GenerateSerializer, Immutable]
public sealed record AvatarEffectSelectedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
