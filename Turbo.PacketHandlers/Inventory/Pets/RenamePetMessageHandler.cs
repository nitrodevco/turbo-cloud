using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

/// <summary>
/// The info stand's rename dialog. A pet standing in the room the player is in is the room's to
/// rename, where everyone sees it under its new name; otherwise it is asked of the inventory.
/// Each refuses a pet that is not the player's and a name a pet may not have, and the dialog is
/// answered either way.
/// </summary>
public class RenamePetMessageHandler(IGrainFactory grainFactory) : IMessageHandler<RenamePetMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RenamePetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var renamed =
            (
                ctx.RoomId > 0
                && await _grainFactory
                    .GetRoomGrain(ctx.RoomId)
                    .RenamePetAsync(ctx.AsActionContext(), message.PetId, message.Name, ct)
                    .ConfigureAwait(false)
            )
            || await _grainFactory
                .GetInventoryGrain(ctx.PlayerId)
                .RenamePetAsync(message.PetId, message.Name, ct)
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new PetRenameResultMessageComposer { PetId = message.PetId, Success = renamed },
                ct
            )
            .ConfigureAwait(false);
    }
}
