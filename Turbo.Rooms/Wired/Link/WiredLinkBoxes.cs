using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Wired.Link;

/// <summary>What the WIRED Links send and receive boxes share: their Link picks and the send itself.</summary>
public static class WiredLinkBoxes
{
    /// <summary>"Select at least one WIRED Link." (refuses a pick that is not a Link: official client capture 2026-10-21).</summary>
    public const string REQUIRE_LINK_FURNI = "wiredfurni.error.require_link_furni";

    /// <summary>Most rows a dataframe holds (AS3 <c>LinkDataframeCodec.MAX_ROWS</c>).</summary>
    public const int MAX_ROWS = 12;

    public static bool IsLink(IRoomItem item) => item.Logic is FurnitureWiredLinkLogic;

    /// <summary>
    /// Sends <paramref name="payload"/> through the picked Links: each owner's network decides
    /// where it goes. Answers how many Links received it.
    /// </summary>
    public static async Task<int> SendAsync(
        IGrainFactory grainFactory,
        RoomId roomId,
        IEnumerable<IRoomFloorItem> links,
        WiredLinkPayload payload,
        CancellationToken ct
    )
    {
        var delivered = 0;
        var used = links.Where(IsLink).ToList();

        // A Link blinks each time something is sent through it, received or not (official client
        // capture 2026-10-21: its state flips with "Receive messages sent by this room" off too).
        foreach (var link in used)
            await ((FurnitureWiredLinkLogic)link.Logic).FlipAsync();

        foreach (var byOwner in used.GroupBy(x => x.OwnerId))
        {
            delivered += await grainFactory
                .GetWiredLinkOwnerGrain(byOwner.Key)
                .SendAsync(roomId, [.. byOwner.Select(x => x.ObjectId.Value)], payload, ct);
        }

        return delivered;
    }

    /// <summary>
    /// Whether a receive trigger hears what arrived: through one of the Links it picked or, when
    /// its Links come from a selector, through any Link of the room (the selector runs only once
    /// the stack fires; inference).
    /// </summary>
    public static bool ListensTo(FurnitureWiredLogic box, WiredLinkReceivedEvent received)
    {
        var sources = box.GetFurniSources();

        if (
            sources.Count > 0
            && sources[0] is { Length: > 0 } slot
            && slot[0] == WiredFurniSourceType.SelectorItems
        )
            return true;

        return box.GetStuffIds().Any(received.LinkIds.Contains);
    }

    public static int Utf8Length(string text) => Encoding.UTF8.GetByteCount(text);

    /// <summary><paramref name="text"/> cut to at most <paramref name="maxBytes"/> UTF-8 bytes, whole characters only.</summary>
    public static string TruncateUtf8(string text, int maxBytes)
    {
        if (Utf8Length(text) <= maxBytes)
            return text;

        var builder = new StringBuilder();
        var bytes = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;

            if (bytes > maxBytes)
                break;

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>The links a send or receive box picked, in the room.</summary>
    public static ImmutableHashSet<int> LinkIds(IEnumerable<IRoomFloorItem> items) =>
        [.. items.Where(IsLink).Select(x => x.ObjectId.Value)];
}
