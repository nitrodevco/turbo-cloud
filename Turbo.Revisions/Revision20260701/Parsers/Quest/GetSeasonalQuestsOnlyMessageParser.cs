using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Quest;

internal class GetSeasonalQuestsOnlyMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetSeasonalQuestsOnlyMessage();
}
