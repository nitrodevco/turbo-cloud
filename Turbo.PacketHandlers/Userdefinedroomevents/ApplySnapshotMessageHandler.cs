using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// The wired editor's "apply furni to set conditions" button: the box remembers its picked
/// furni as they stand now, without the player saving the box again.
///
/// Nothing is sent back. The editor stays open and expects no answer; the save-success message
/// would close it.
/// </summary>
public class ApplySnapshotMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ApplySnapshotMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ApplySnapshotMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.Id <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ApplyWiredSnapshotAsync(ctx.AsActionContext(), message.Id, ct)
            .ConfigureAwait(false);
    }
}
