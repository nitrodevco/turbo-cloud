using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Pets;

internal class PetCommandsMessageComposerSerializer(int header)
    : AbstractSerializer<PetCommandsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetCommandsMessageComposer message)
    {
        packet.WriteInteger(message.PetId).WriteInteger(message.AllCommands.Length);

        foreach (var command in message.AllCommands)
            packet.WriteInteger((int)command);

        packet.WriteInteger(message.EnabledCommands.Length);

        foreach (var command in message.EnabledCommands)
            packet.WriteInteger((int)command);
    }
}
