using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class WithdrawCreditVaultMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new WithdrawCreditVaultMessage();
}
