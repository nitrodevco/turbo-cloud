using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.NewNavigator;

internal class NewNavigatorPreferencesMessageSerializer(int header)
    : AbstractSerializer<NewNavigatorPreferencesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NewNavigatorPreferencesMessageComposer message
    )
    {
        packet.WriteInteger(message.WindowX);
        packet.WriteInteger(message.WindowY);
        packet.WriteInteger(message.WindowWidth);
        packet.WriteInteger(message.WindowHeight);
        packet.WriteBoolean(message.LeftPaneHidden);
        packet.WriteInteger(message.ResultsMode);
    }
}
