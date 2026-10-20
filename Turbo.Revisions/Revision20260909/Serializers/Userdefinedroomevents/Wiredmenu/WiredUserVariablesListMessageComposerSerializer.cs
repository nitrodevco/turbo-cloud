using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// AS3 <c>WiredUserVariablesPage</c>: each holder as <c>WiredUserVariablesElement</c>, whose value
/// carries no variable id (<c>WiredVariableStorageParameter</c> read without it).
/// </summary>
internal class WiredUserVariablesListMessageComposerSerializer(int header)
    : AbstractSerializer<WiredUserVariablesListMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredUserVariablesListMessageComposer message
    )
    {
        var page = message.Page;

        packet
            .WriteString(page.VariableId)
            .WriteInteger(page.TotalEntries)
            .WriteInteger(page.Page)
            .WriteInteger(page.PageSize)
            .WriteInteger(page.Owners.Length);

        foreach (var owner in page.Owners)
        {
            packet
                .WriteInteger(owner.EntityType)
                .WriteInteger(owner.EntityId)
                .WriteString(owner.EntityName);

            WiredStoredVariableValueSerializer.Serialize(
                packet,
                owner.Value,
                withVariableId: false
            );
        }

        packet.WriteInteger(page.UserTypeFilter).WriteInteger(page.SortType);
    }
}
