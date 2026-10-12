using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Navigator;

/// <summary>
/// <c>navigator/goto/&lt;name&gt;</c> links the server resolves: <c>predefined_noob_lobby</c> (the
/// new user lobby offer, the toolbar and leaving a room as a new user) goes to one of the hotel's
/// new user lobbies, or nowhere when it has none; anything else (<c>random_friending_room</c>) to a
/// random room with people in it.
/// </summary>
public class ForwardToSomeRoomMessageHandler(
    IGrainFactory grainFactory,
    INoobLobbyDirectory noobLobbies
) : IMessageHandler<ForwardToSomeRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ForwardToSomeRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        RoomId? roomId =
            message.ForwardData == INoobLobbyDirectory.FORWARD_DATA
                ? await noobLobbies.PickAsync(ct).ConfigureAwait(false)
                : await _grainFactory
                    .GetRoomDirectoryGrain()
                    .GetRandomPopulatedRoomAsync(ct)
                    .ConfigureAwait(false);

        if (roomId is not null && roomId.Value > 0)
        {
            await _grainFactory
                .ForwardPlayerToRoomAsync(ctx.PlayerId, roomId.Value, ct)
                .ConfigureAwait(false);
        }
    }
}
