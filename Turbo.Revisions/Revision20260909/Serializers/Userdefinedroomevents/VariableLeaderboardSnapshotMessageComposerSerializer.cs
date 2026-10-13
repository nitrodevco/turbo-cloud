using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>
/// Flash <c>VariableLeaderboardSnapshotMessageParser</c> + <c>VariableLeaderboardDescriptorData.fromMessage</c>:
/// int n (0..4); per leaderboard int overlay, int query revision, string title, string description,
/// int tile x, int tile y, int x offset, int altitude, int sort mode, int c (1..4), c x (int type,
/// string label, int width), int k (0..10), k x (string variable id, string display name), string
/// default variable id, int page size option.
/// </summary>
internal class VariableLeaderboardSnapshotMessageComposerSerializer(int header)
    : AbstractSerializer<VariableLeaderboardSnapshotMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        VariableLeaderboardSnapshotMessageComposer message
    )
    {
        packet.WriteInteger(message.Descriptors.Length);

        foreach (var descriptor in message.Descriptors)
        {
            packet
                .WriteInteger(descriptor.OverlayInstanceId)
                .WriteInteger(descriptor.QueryRevision)
                .WriteString(descriptor.Title)
                .WriteString(descriptor.Description)
                .WriteInteger(descriptor.TileX)
                .WriteInteger(descriptor.TileY)
                .WriteInteger(descriptor.OffsetX)
                .WriteInteger(descriptor.Altitude)
                .WriteInteger(descriptor.SortMode)
                .WriteInteger(descriptor.Columns.Length);

            foreach (var column in descriptor.Columns)
                packet
                    .WriteInteger(column.Type)
                    .WriteString(column.Label)
                    .WriteInteger(column.Width);

            packet.WriteInteger(descriptor.Choices.Length);

            foreach (var choice in descriptor.Choices)
                packet.WriteString(choice.VariableId).WriteString(choice.DisplayName);

            packet
                .WriteString(descriptor.DefaultVariableId)
                .WriteInteger(descriptor.PageSizeOption);
        }
    }
}
