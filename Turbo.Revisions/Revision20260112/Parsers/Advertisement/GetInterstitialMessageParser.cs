using Turbo.Primitives.Messages.Incoming.Advertisement;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Advertisement;

internal class GetInterstitialMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetInterstitialMessage();
}
