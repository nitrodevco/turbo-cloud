using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Catalog;

/// <summary>
/// <c>ClaimProductMessageComposer(claimId, choice = -1)</c>: the choice is only written when it is
/// not -1 (the special items display sends the id alone, the adoption window adds 0 or 1).
/// </summary>
internal class ClaimProductMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ClaimProductMessage
        {
            ClaimId = packet.PopString(),
            Choice = packet.Remaining > 0 ? packet.PopInt() : -1,
        };
}
