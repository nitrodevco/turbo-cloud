using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userclassification;
using Turbo.Primitives.Messages.Outgoing.Userclassification;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Userclassification;

/// <summary>
/// <c>:uc &lt;class&gt;</c> and <c>:anew</c> list the players in the sender's room by class. The
/// client sends <c>:anew</c> for an ambassador or at security level 4, <c>:uc</c> at level 4 alone
/// (<c>ChatInputWidgetHandler.as:403-421</c>), so the gate is the ambassador alert's and the
/// service keeps an ambassador to new users.
/// </summary>
[RequiresPermission(PermissionNodes.Role.AMBASSADOR, PermissionNodes.Room.MODERATE_ANY)]
public class RoomUsersClassificationMessageHandler(IUserClassificationService classification)
    : IMessageHandler<RoomUsersClassificationMessage>
{
    private readonly IUserClassificationService _classification = classification;

    public async ValueTask HandleAsync(
        RoomUsersClassificationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        var users = await _classification
            .ClassifyRoomAsync(ctx.PlayerId, ctx.RoomId, message.Classification, ct)
            .ConfigureAwait(false);

        if (users is not { } listed)
            return;

        await ctx.SendComposerAsync(new UserClassificationMessageComposer { Users = listed }, ct)
            .ConfigureAwait(false);
    }
}
