using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class CreateGuildMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CreateGuildMessage
        {
            Request = new GuildCreationRequestSnapshot
            {
                Name = packet.PopString(),
                Description = packet.PopString(),
                RoomId = RoomId.Parse(packet.PopInt()),
                PrimaryColorId = packet.PopInt(),
                SecondaryColorId = packet.PopInt(),
                BadgeParts = GuildBadgePartParser.Parse(packet),
            },
        };
}
