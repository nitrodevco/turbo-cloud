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
    public static ImmutableArray<CfhChatLineSnapshot> Parse(IClientPacket packet) =>
        [
            .. packet.PopList(bytesPerItem: 6, p => new CfhChatLineSnapshot
            {
                PlayerId = p.PopInt(),
                Text = p.PopString(),
            }),
        ];
}
