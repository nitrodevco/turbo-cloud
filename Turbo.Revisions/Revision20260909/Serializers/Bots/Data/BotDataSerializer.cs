using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Bots.Data;

/// <summary>The bot row shared by the inventory list and bot arrivals; gender comes before figure.</summary>
internal static class BotDataSerializer
{
    public static void Serialize(IServerPacket packet, BotSnapshot bot)
    {
        packet
            .WriteInteger(bot.Id)
            .WriteString(bot.Name)
            .WriteString(bot.Motto)
            .WriteString(bot.Gender.ToLegacyString())
            .WriteString(bot.Figure);
    }
}
