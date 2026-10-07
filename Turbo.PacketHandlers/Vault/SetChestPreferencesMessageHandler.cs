using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Saves a chest's settings window; the chest checks the modes it is sent.</summary>
public class SetChestPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetChestPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetChestPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new SetChestPreferencesInteraction
                {
                    Name = message.Name,
                    Description = message.Description,
                    EveryoneCanOpen = message.EveryoneCanOpen,
                    EveryoneCanDonate = message.EveryoneCanDonate,
                    StateMode = (WiredChestStateMode)message.StateControlMode,
                    PreviewMode = (WiredChestPreviewMode)message.PreviewMode,
                    PreviewAmount = message.PreviewAmount,
                    WiredEnabled = message.WiredEnabled,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
