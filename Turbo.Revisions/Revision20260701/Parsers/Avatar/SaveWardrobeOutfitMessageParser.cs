using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260701.Parsers.Avatar;

internal class SaveWardrobeOutfitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SaveWardrobeOutfitMessage
        {
            SlotId = packet.PopInt(),
            Figure = packet.PopString(),
            Gender = AvatarGenderTypeExtensions.FromLegacyString(packet.PopString()),
        };
}
