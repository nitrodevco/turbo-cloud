using Turbo.Primitives.Messages.Incoming.Room.Bots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Bots;

internal class GetBotCommandConfigurationDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetBotCommandConfigurationDataMessage();
}
