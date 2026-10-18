using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// Room Linker link networks are not open on the live hotel: it answers every link request,
/// whichever linker and room it is for, with "This Link is no longer available in the room"
/// and creates nothing, so the link window never gets a configuration to show.
/// </summary>
public class WiredLinkRequestMessageHandler : IMessageHandler<WiredLinkRequestMessage>
{
    public async ValueTask HandleAsync(
        WiredLinkRequestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await ctx.SendComposerAsync(
                new WiredLinkOperationResultMessageComposer
                {
                    RoomId = ctx.RoomId.Value,
                    FurniId = message.FurniId,
                    Operation = message.Operation,
                    ResultCode = WiredLinkResultCodes.LINK_UNAVAILABLE,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
