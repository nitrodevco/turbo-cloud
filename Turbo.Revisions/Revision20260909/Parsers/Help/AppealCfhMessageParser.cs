using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>The report status window's appeal button: the report's id, as an int.</summary>
internal class AppealCfhMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new AppealCfhMessage { ReportId = packet.PopInt() };
}
