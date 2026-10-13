using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Crafting;

/// <summary>outgoing/crafting/CraftComposer.as:17: the table's id, the recipe code.</summary>
internal class CraftMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CraftMessage { ObjectId = packet.PopInt(), RecipeCode = packet.PopString() };
}
