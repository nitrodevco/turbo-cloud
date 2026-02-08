using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Collectibles;

internal class NftCollectiblesClaimRewardItemMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new NftCollectiblesClaimRewardItemMessage();
}
