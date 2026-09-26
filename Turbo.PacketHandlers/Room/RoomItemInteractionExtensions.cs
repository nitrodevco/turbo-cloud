using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.PacketHandlers.Room;

/// <summary>
/// Handing a dedicated furniture action to the room the sender is standing in.
///
/// Every packet that throws a dice, opens a present, rents a space or dresses a mannequin is the
/// same three checks and the same grain call with a different interaction. The room decides
/// whether the item answers to it and whether this player may use it; the handler only states
/// which action was asked for, plus any check on its own payload.
/// </summary>
internal static class RoomItemInteractionExtensions
{
    /// <summary>
    /// Sends the interaction to the sender's current room, or nothing when there is no player,
    /// no room or no item. Returns whether the room carried it out.
    /// </summary>
    public static async Task<bool> InteractWithRoomItemAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        RoomObjectId objectId,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || objectId <= 0)
            return false;

        return await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(ctx.AsActionContext(), objectId, interaction, ct)
            .ConfigureAwait(false);
    }
}
