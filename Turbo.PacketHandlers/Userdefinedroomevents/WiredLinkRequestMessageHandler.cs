using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// Every request of a WIRED Link's window: its configuration, the Link's save, and creating,
/// renaming, deleting or listing the connections of one of the owner's networks. Only the Link's
/// owner gets anywhere (<c>wiredlink.error.1</c>); a furni that is not a Link in the room answers
/// <c>wiredlink.error.7</c>.
/// </summary>
public class WiredLinkRequestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredLinkRequestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredLinkRequestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        var roomId = ctx.RoomId.Value;
        var (resultCode, isBuildersClub) = await _grainFactory
            .GetRoomGrain(roomId)
            .CheckWiredLinkAsync(ctx.AsActionContext(), message.FurniId, ct)
            .ConfigureAwait(false);

        if (resultCode != WiredLinkResultCodes.OK)
        {
            await SendResultAsync(ctx, message, WiredLinkOperationOutcome.Failed(resultCode), ct)
                .ConfigureAwait(false);

            return;
        }

        var owner = _grainFactory.GetWiredLinkOwnerGrain(ctx.PlayerId);
        var link = new WiredLinkEndpoint(roomId, message.FurniId);

        switch (message.Operation)
        {
            case WiredLinkOperations.GET_CONFIGURATION:
            {
                var configuration = await owner
                    .GetConfigurationAsync(link, ct)
                    .ConfigureAwait(false);

                await ctx.SendComposerAsync(
                        new WiredLinkConfigurationMessageComposer
                        {
                            RoomId = roomId,
                            FurniId = message.FurniId,
                            SelectedNetworkId = configuration.SelectedNetworkId,
                            ReceiveLocal = configuration.ReceiveLocal,
                            Networks = configuration.Networks,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return;
            }
            case WiredLinkOperations.GET_CONNECTIONS:
            {
                var connections = await owner
                    .GetConnectionsAsync(message.NetworkId, ct)
                    .ConfigureAwait(false);

                if (connections is null)
                {
                    await SendResultAsync(
                            ctx,
                            message,
                            WiredLinkOperationOutcome.Failed(
                                WiredLinkResultCodes.NETWORK_NOT_FOUND
                            ),
                            ct
                        )
                        .ConfigureAwait(false);

                    return;
                }

                await ctx.SendComposerAsync(
                        new WiredLinkConnectionsMessageComposer
                        {
                            RoomId = roomId,
                            FurniId = message.FurniId,
                            NetworkId = message.NetworkId,
                            Connections = connections.Value,
                        },
                        ct
                    )
                    .ConfigureAwait(false);

                return;
            }
        }

        var outcome = message.Operation switch
        {
            WiredLinkOperations.SAVE => await owner
                .SaveAsync(link, isBuildersClub, message.NetworkId, message.ReceiveLocal, ct)
                .ConfigureAwait(false),
            WiredLinkOperations.CREATE_NETWORK => await owner
                .CreateNetworkAsync(link, message.Name, ct)
                .ConfigureAwait(false),
            WiredLinkOperations.RENAME_NETWORK => await owner
                .RenameNetworkAsync(link, message.NetworkId, message.Name, ct)
                .ConfigureAwait(false),
            WiredLinkOperations.DELETE_NETWORK => await owner
                .DeleteNetworkAsync(link, message.NetworkId, ct)
                .ConfigureAwait(false),
            _ => null,
        };

        if (outcome is null)
            return;

        await SendResultAsync(ctx, message, outcome, ct).ConfigureAwait(false);

        // The owner's other Links in this room that a delete disconnected.
        foreach (var disconnected in outcome.Disconnected)
        {
            if (disconnected.RoomId != roomId)
                continue;

            await ctx.SendComposerAsync(
                    new WiredLinkConfigurationInvalidatedMessageComposer
                    {
                        RoomId = roomId,
                        FurniId = disconnected.FurniId,
                        DeletedNetworkId = outcome.RemovedNetworkId,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }

    private static Task SendResultAsync(
        MessageContext ctx,
        WiredLinkRequestMessage message,
        WiredLinkOperationOutcome outcome,
        CancellationToken ct
    ) =>
        ctx.SendComposerAsync(
            new WiredLinkOperationResultMessageComposer
            {
                RoomId = ctx.RoomId.Value,
                FurniId = message.FurniId,
                Operation = message.Operation,
                ResultCode = outcome.ResultCode,
                SelectedNetworkId = outcome.SelectedNetworkId,
                ReceiveLocal = outcome.ReceiveLocal,
                RemovedNetworkId = outcome.RemovedNetworkId,
                Networks = outcome.Networks,
            },
            ct
        );
}
