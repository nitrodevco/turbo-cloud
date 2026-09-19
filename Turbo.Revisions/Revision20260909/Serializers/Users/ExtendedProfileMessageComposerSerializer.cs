using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class ExtendedProfileMessageComposerSerializer(int header)
    : AbstractSerializer<ExtendedProfileMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ExtendedProfileMessageComposer message)
    {
        var profile = message.Profile;
        var badges = message.Badges;

        packet
            .WriteInteger((int)profile.UserId)
            .WriteString(profile.UserName)
            .WriteString(profile.Figure)
            .WriteString(profile.Motto)
            .WriteString(profile.CreationDate)
            .WriteInteger(profile.AchievementScore)
            .WriteInteger(profile.FriendCount)
            .WriteBoolean(profile.IsFriend)
            .WriteBoolean(profile.IsFriendRequestSent)
            .WriteBoolean(profile.IsOnline)
            .WriteInteger(profile.Guilds.Count);

        foreach (var guild in profile.Guilds)
            GuildInfoSerializer.Serialize(packet, guild);

        packet
            .WriteInteger(profile.LastAccessSinceInSeconds)
            .WriteBoolean(profile.OpenProfileWindow)
            .WriteBoolean(profile.IsHidden)
            .WriteInteger(profile.AccountLevel)
            .WriteInteger(profile.IntegerField24)
            .WriteInteger(profile.StarGemCount)
            .WriteBoolean(profile.BooleanField26)
            .WriteBoolean(profile.BooleanField27)
            .WriteInteger(badges.TotalBadges)
            .WriteInteger(profile.AchievementLevel)
            .WriteInteger(badges.RarityCounts.Length);

        foreach (var rarity in badges.RarityCounts)
            packet.WriteByte(rarity.RarityId).WriteInteger(rarity.Count);

        packet.WriteInteger(badges.TotalBadgesRank);
    }
}
