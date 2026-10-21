using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<(int ResultCode, bool IsBuildersClub)> CheckWiredLinkAsync(
        ActionContext ctx,
        int furniId,
        CancellationToken ct
    )
    {
        if (
            !FurniModule.TryGetFloorItem(furniId, out var item)
            || item.Logic is not FurnitureWiredLinkLogic
        )
            return Task.FromResult((WiredLinkResultCodes.LINK_UNAVAILABLE, false));

        if (item.OwnerId != ctx.PlayerId)
            return Task.FromResult((WiredLinkResultCodes.NOT_OWNER, item.IsBuildersClub));

        return Task.FromResult((WiredLinkResultCodes.OK, item.IsBuildersClub));
    }

    // The speakers' bubbles come first, then the receive triggers fire, in the same tick
    // (official client capture 2026-10-21: Chat, then the boxes' state flips, same ms).
    public async Task ReceiveWiredLinkPayloadAsync(
        ImmutableArray<int> linkIds,
        WiredLinkPayload payload,
        CancellationToken ct
    )
    {
        await WiredSystem.SpeakLinkPayloadAsync(linkIds, payload, ct);

        await PublishRoomEventAsync(
            new WiredLinkReceivedEvent
            {
                RoomId = _state.RoomId,
                CausedBy = ActionContext.CreateForWired(_state.RoomId),
                LinkIds = linkIds,
                Payload = payload,
            },
            ct
        );
    }
}
