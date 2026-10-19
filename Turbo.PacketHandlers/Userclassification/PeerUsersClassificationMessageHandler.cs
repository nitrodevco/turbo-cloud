using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userclassification;
using Turbo.Primitives.Messages.Outgoing.Userclassification;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Userclassification;

/// <summary>
/// <c>:uc hotel &lt;class&gt;</c> lists the players online by class. The client sends it at security
/// level 4 (<c>ChatInputWidgetHandler.as:403-414</c>), which <c>room.moderate.any</c> gives.
/// </summary>
[RequiresPermission(PermissionNodes.Room.MODERATE_ANY)]
public class PeerUsersClassificationMessageHandler(IUserClassificationService classification)
    : IMessageHandler<PeerUsersClassificationMessage>
{
    private readonly IUserClassificationService _classification = classification;

    public async ValueTask HandleAsync(
        PeerUsersClassificationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var users = await _classification
            .ClassifyHotelAsync(message.Classification, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new UserClassificationMessageComposer { Users = users }, ct)
            .ConfigureAwait(false);
    }
}
