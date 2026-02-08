using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Camera;

internal class PhotoCompetitionMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new PhotoCompetitionMessage();
}
