using System.Collections.Immutable;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// The chat lines a call for help carries: a count, then each line's user id and text
/// (<c>ChatReportController.collectSelectedEntries</c>, whose length / 2 is the count).
/// </summary>
internal static class CfhChatLinesParser
{
    public static ImmutableArray<CfhChatLineSnapshot> Parse(IClientPacket packet)
    {
        var count = packet.PopInt();
        var lines = ImmutableArray.CreateBuilder<CfhChatLineSnapshot>();

        // The count is the client's: a line is read only while the packet still holds one.
        for (var i = 0; i < count && packet.Remaining > 0; i++)
            lines.Add(
                new CfhChatLineSnapshot { PlayerId = packet.PopInt(), Text = packet.PopString() }
            );

        return lines.ToImmutable();
    }
}
