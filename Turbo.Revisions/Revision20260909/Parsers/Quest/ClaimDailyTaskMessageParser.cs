using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Quest;

/// <summary>A daily task's Claim button: the task id, as an int (AS3 <c>ClaimDailyTaskComposer</c>).</summary>
internal class ClaimDailyTaskMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ClaimDailyTaskMessage { TaskId = packet.PopInt() };
}
