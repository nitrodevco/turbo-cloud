using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class LockAllChestsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var shouldLock = packet.PopBoolean();
        var all = packet.PopBoolean();

        return new LockAllChestsMessage { Lock = shouldLock, All = all };
    }
}
