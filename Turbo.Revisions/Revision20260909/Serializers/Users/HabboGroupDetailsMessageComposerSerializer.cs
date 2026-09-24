using System.Globalization;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class HabboGroupDetailsMessageComposerSerializer(int header)
    : AbstractSerializer<HabboGroupDetailsMessageComposer>(header)
{
    /// <summary>
    /// The client prints the creation date as it is given and parses nothing out of it, so the
    /// format is the hotel's to pick.
    /// </summary>
    private const string CREATION_DATE_FORMAT = "dd-MM-yyyy";

    protected override void Serialize(
        IServerPacket packet,
        HabboGroupDetailsMessageComposer message
    )
    {
        var view = message.View;
        var guild = view.Guild;

        packet
            .WriteInteger(guild.GuildId)
            // A group a player made is a guild; the flag is there for the hotel's own groups,
            // which have no homeroom and cannot be left.
            .WriteBoolean(true)
            .WriteInteger((int)guild.Type)
            .WriteString(guild.Name)
            .WriteString(view.Description)
            .WriteString(guild.BadgeCode)
            .WriteInteger(guild.RoomId)
            .WriteString(message.RoomName)
            .WriteInteger((int)view.Status)
            .WriteInteger(view.MemberCount)
            .WriteBoolean(message.IsFavourite)
            .WriteString(
                view.CreatedAt.ToString(CREATION_DATE_FORMAT, CultureInfo.InvariantCulture)
            )
            .WriteBoolean(view.IsOwner)
            .WriteBoolean(view.IsAdmin)
            .WriteString(message.OwnerName)
            .WriteBoolean(message.OpenDetails)
            .WriteBoolean(view.MembersCanDecorate)
            .WriteInteger(view.PendingMemberCount)
            .WriteBoolean(guild.HasForum);
    }
}
