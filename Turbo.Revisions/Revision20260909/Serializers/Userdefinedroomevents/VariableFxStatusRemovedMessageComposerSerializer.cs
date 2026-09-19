using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class VariableFxStatusRemovedMessageComposerSerializer(int header)
    : AbstractSerializer<VariableFxStatusRemovedMessageComposer>(header)
{
    // The client only tests the kind for "u"; anything else is a furni. "f" says what it is.
    private const string USER_KIND = "u";
    private const string FURNI_KIND = "f";

    protected override void Serialize(
        IServerPacket packet,
        VariableFxStatusRemovedMessageComposer message
    )
    {
        packet.WriteInteger(message.Keys.Length);

        foreach (var key in message.Keys)
        {
            var kind = key.IsUserEntity ? USER_KIND : FURNI_KIND;

            packet.WriteString($"{key.ConfigId}|{key.VariableId}|{kind}|{key.EntityId}");
        }
    }
}
