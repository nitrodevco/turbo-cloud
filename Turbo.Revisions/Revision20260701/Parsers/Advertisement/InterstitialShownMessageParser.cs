using Turbo.Primitives.Messages.Incoming.Advertisement;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Advertisement;

internal class InterstitialShownMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new InterstitialShownMessage();
}
