using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class UserChangeMessageComposerSerializer(int header)
    : AbstractSerializer<UserChangeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserChangeMessageComposer message)
    {
        packet
            .WriteInteger(message.ObjectId)
            .WriteString(message.Figure)
            // Lower case, unlike FigureUpdate's (official client captures: UserChange 18 of 18
            // "m" or "f"; FigureUpdate "M").
            .WriteString(message.Gender.ToLegacyString().ToLowerInvariant())
            .WriteString(message.CustomInfo)
            .WriteInteger(message.AchievementScore)
            .WriteString("")
            .WriteInteger(0)
            .WriteInteger(message.BadgesRank);
    }
}
