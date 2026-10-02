using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Turbo;

internal class TurboCommandSuggestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new TurboCommandSuggestMessage
        {
            RequestId = packet.PopInt(),
            Command = Bounded(packet.PopString()),
            Parameter = packet.PopInt(),
            Prefix = Bounded(packet.PopString()),
            Syntax = Bounded(packet.PopString()),
            ArgumentText = Context(packet.PopString()),
        };

    private static string Bounded(string text) =>
        text.Length <= ClientCapabilities.MAX_SUGGEST_TEXT
            ? text
            : text[..ClientCapabilities.MAX_SUGGEST_TEXT];

    private static string Context(string text) =>
        text.Length <= ClientCapabilities.MAX_SUGGEST_CONTEXT ? text : string.Empty;
}
