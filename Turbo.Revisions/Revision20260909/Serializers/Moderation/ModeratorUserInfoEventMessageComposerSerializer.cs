using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class ModeratorUserInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorUserInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorUserInfoEventMessageComposer message
    )
    {
        var user = message.User;

        packet
            .WriteInteger(user.UserId)
            .WriteString(user.UserName)
            .WriteString(user.Figure)
            .WriteInteger(user.RegistrationAgeInMinutes)
            .WriteInteger(user.MinutesSinceLastLogin)
            .WriteBoolean(user.Online)
            .WriteInteger(user.CfhCount)
            .WriteInteger(user.AbusiveCfhCount)
            .WriteInteger(user.CautionCount)
            .WriteInteger(user.BanCount)
            .WriteInteger(user.TradingLockCount)
            .WriteString(user.TradingExpiryDate)
            .WriteString(user.LastPurchaseDate)
            .WriteInteger(user.IdentityId)
            .WriteInteger(user.IdentityRelatedBanCount)
            .WriteString(user.PrimaryEmailAddress)
            .WriteString(user.UserClassification)
            .WriteString(user.LastSanctionTime)
            .WriteInteger(user.SanctionAgeHours);
    }
}
