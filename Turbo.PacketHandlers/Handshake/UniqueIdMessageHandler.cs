using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Handshake;

/// <summary>
/// The client's machine id, sent in the handshake before its ticket. Kept on the connection so
/// the login it opens is recorded with it: one machine behind many accounts is what staff look
/// for when a banned player comes back.
/// </summary>
public class UniqueIdMessageHandler : IMessageHandler<UniqueIdMessage>
{
    public ValueTask HandleAsync(UniqueIdMessage message, MessageContext ctx, CancellationToken ct)
    {
        var machineId = message.MachineID.Trim();

        if (machineId.Length > 0)
            ctx.SetMachineId(
                machineId.Length > PlayerLogins.MACHINE_ID_MAX_LENGTH
                    ? machineId[..PlayerLogins.MACHINE_ID_MAX_LENGTH]
                    : machineId
            );

        return ValueTask.CompletedTask;
    }
}
