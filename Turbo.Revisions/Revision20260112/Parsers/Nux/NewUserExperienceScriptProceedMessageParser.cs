using Turbo.Primitives.Messages.Incoming.Nux;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Nux;

internal class NewUserExperienceScriptProceedMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new NewUserExperienceScriptProceedMessage();
}
