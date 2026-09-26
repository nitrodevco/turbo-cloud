using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.Avatar;

public record SaveWardrobeOutfitMessage : IMessageEvent
{
    public required int SlotId { get; init; }
    public required string Figure { get; init; }
    public required AvatarGenderType Gender { get; init; }
}
