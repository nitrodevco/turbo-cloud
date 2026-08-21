using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Crafting;

internal class CraftSecretMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CraftSecretMessage();
}
