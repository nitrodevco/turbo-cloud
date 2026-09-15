using Turbo.Primitives.Messages.Incoming.Gifts;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Gifts;

internal class SetPhoneNumberVerificationStatusMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetPhoneNumberVerificationStatusMessage();
}
