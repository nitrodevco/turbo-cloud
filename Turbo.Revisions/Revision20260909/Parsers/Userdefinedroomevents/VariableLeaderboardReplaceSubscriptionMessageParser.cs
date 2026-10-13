using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

/// <summary>
/// Flash <c>VariableLeaderboardReplaceSubscriptionMessageComposer</c>: int overlay, int query
/// revision, string variable, int n, n x int chunk index. The client never sends more than two;
/// a longer list is cut to its first two and the handler refuses what Flash would not send.
/// </summary>
internal class VariableLeaderboardReplaceSubscriptionMessageParser : IParser
{
    private const int MAX_CHUNKS = 2;

    public IMessageEvent Parse(IClientPacket packet)
    {
        var overlayInstanceId = packet.PopInt();
        var queryRevision = packet.PopInt();
        var selectedVariableId = packet.PopString();
        var count = packet.PopInt();
        var chunks = ImmutableArray.CreateBuilder<int>();

        for (var i = 0; i < count && i < MAX_CHUNKS; i++)
            chunks.Add(packet.PopInt());

        return new VariableLeaderboardReplaceSubscriptionMessage
        {
            OverlayInstanceId = overlayInstanceId,
            QueryRevision = queryRevision,
            SelectedVariableId = selectedVariableId,
            ChunkIndices = count > MAX_CHUNKS || count < 0 ? [-1] : chunks.ToImmutable(),
        };
    }
}
