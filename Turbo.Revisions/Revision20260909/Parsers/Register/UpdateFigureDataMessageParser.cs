using Turbo.Primitives.Messages.Incoming.Register;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Parsers.Register;

internal class UpdateFigureDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateFigureDataMessage
        {
            Gender = AvatarGenderTypeExtensions.FromLegacyString(packet.PopString()),
            Figure = packet.PopString(),
        };
}
