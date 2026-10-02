using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Session;

/// <summary>
/// Direct entry request: home room button, a submitted door password, or a doorbell ring.
/// </summary>
public class OpenFlatConnectionMessageHandler(IRoomService roomService, IGrainFactory grainFactory)
    : IMessageHandler<OpenFlatConnectionMessage>
{
    private readonly IRoomService _roomService = roomService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public ValueTask HandleAsync(
        OpenFlatConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        new(
            RoomTelemetry.MeasureAsync(
                RoomTelemetry.DIRECT_ENTRY,
                message.RoomId,
                () => HandleEntryAsync(message, ctx, ct)
            )
        );

    private async Task HandleEntryAsync(
        OpenFlatConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var access = await _roomService
            .CheckRoomEntryAccessAsync(
                ctx.PlayerId,
                message.RoomId,
                message.Password,
                bypassDoor: await _grainFactory
                    .IsArrivingByTeleportAsync(ctx.PlayerId, message.RoomId, ct)
                    .ConfigureAwait(false),
                ct
            )
            .ConfigureAwait(false);

        RoomTelemetry.RecordEntryAccess(access);

        await _roomService
            .OpenRoomForPlayerIdAsync(
                ctx.AsActionContext(),
                ctx.PlayerId,
                message.RoomId,
                access,
                ct
            )
            .ConfigureAwait(false);
    }
}
