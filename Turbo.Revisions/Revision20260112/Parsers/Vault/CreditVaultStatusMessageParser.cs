using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Vault;

internal class CreditVaultStatusMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CreditVaultStatusMessage();
}
