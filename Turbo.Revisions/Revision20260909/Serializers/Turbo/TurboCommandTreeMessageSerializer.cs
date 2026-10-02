using System.Collections.Immutable;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

/// <summary>The shape is <c>docs/client-capabilities.md</c>, <c>chat.commands</c>.</summary>
internal class TurboCommandTreeMessageSerializer(int header)
    : AbstractSerializer<TurboCommandTreeMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboCommandTreeMessage message)
    {
        var commands = message.Tree.Commands;

        packet.WriteInteger(commands.Length);

        foreach (var command in commands)
        {
            packet.WriteString(command.Name);
            packet.WriteInteger(command.Aliases.Length);

            foreach (var alias in command.Aliases)
                packet.WriteString(alias);

            packet
                .WriteString(command.Category)
                .WriteString(command.Description)
                .WriteString(command.Usage)
                .WriteInteger(command.RoomLevel)
                .WriteBoolean(command.Operator);

            WriteParameters(packet, command.Parameters);
            packet.WriteInteger(command.Syntax.Length);
            foreach (var syntax in command.Syntax)
            {
                packet.WriteString(syntax.Path).WriteString(syntax.Usage);
                WriteParameters(packet, syntax.Parameters);
            }
        }
    }

    private static void WriteParameters(
        IServerPacket packet,
        ImmutableArray<CommandTreeParameterSnapshot> parameters
    )
    {
        packet.WriteInteger(parameters.Length);

        foreach (var parameter in parameters)
        {
            packet
                .WriteString(parameter.Name)
                .WriteInteger((int)parameter.Kind)
                .WriteBoolean(parameter.Optional)
                .WriteInteger((int)parameter.Suggest)
                .WriteBoolean(parameter.Selectors)
                .WriteInteger(parameter.Members.Length);

            foreach (var member in parameter.Members)
                packet.WriteString(member);
            packet
                .WriteString(parameter.Description)
                .WriteString(parameter.Minimum)
                .WriteString(parameter.Maximum)
                .WriteInteger(parameter.MinLength)
                .WriteInteger(parameter.MaxLength)
                .WriteString(parameter.DefaultValue);
        }
    }
}
