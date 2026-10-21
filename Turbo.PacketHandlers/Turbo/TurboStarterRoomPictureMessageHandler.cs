using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A Turbo extension (<c>starter.rooms</c>): the starter room camera's capture, drawn by the camera
/// renderer into the new user flow's round room picture (<c>--frame starter-room</c>) and the client
/// pointed at it. The room inside the frame is the camera's own view (a PNG), walls and floors
/// textured as the player saw them; the render data gives the furni that break out of the frame,
/// and the whole room when no view came. Only from inside the room, only for one of the hotel's
/// starter rooms, and only for whoever may run <c>:starterroom</c> - staff, whose view is trusted.
/// The capture runs off the packet (<see cref="CameraCaptureJobs"/>), so the render does not hold
/// up the player's session; one sent while their last capture is still being drawn is not ok.
/// </summary>
[RequiresPermission(PermissionNodes.Command.STARTERROOM)]
public class TurboStarterRoomPictureMessageHandler(
    IStarterRoomDirectory directory,
    CameraPhotoStore store,
    ICameraRenderer renderer,
    CameraCaptureJobs jobs
) : IMessageHandler<TurboStarterRoomPictureMessage>
{
    public const string FRAME = "starter-room";

    /// <summary>The largest view taken: a 296x284 PNG is far smaller.</summary>
    private const int MAX_SCENE_BYTES = 1024 * 1024;

    private static readonly byte[] PNG_SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];

    public async ValueTask HandleAsync(
        TurboStarterRoomPictureMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId.Value <= 0 || ctx.RoomId.Value != message.RoomId)
            return;

        var playerId = ctx.PlayerId;
        var roomId = ctx.RoomId;

        if (jobs.TryStart(playerId, token => CaptureAsync(message, playerId, roomId, token)))
            return;

        await ctx.SendComposerAsync(
                new TurboStarterRoomPictureResultMessage { RoomId = message.RoomId, Ok = false },
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task<IComposer> CaptureAsync(
        TurboStarterRoomPictureMessage message,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    ) =>
        new TurboStarterRoomPictureResultMessage
        {
            RoomId = message.RoomId,
            Ok = await TakeAsync(message, playerId, roomId, ct).ConfigureAwait(false),
        };

    private async Task<bool> TakeAsync(
        TurboStarterRoomPictureMessage message,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        if (await directory.GetAsync(roomId, ct).ConfigureAwait(false) is null)
            return false;

        var json = store.Inflate(message.Data);

        if (json is null)
            return false;

        var paths = directory.GetPicturePaths(roomId);

        await File.WriteAllTextAsync(paths.RenderDataPath, json, ct).ConfigureAwait(false);

        var hasScene =
            message.Scene.Length is > 8 and <= MAX_SCENE_BYTES
            && message.Scene.AsSpan(0, PNG_SIGNATURE.Length).SequenceEqual(PNG_SIGNATURE);

        if (hasScene)
            await File.WriteAllBytesAsync(paths.ScenePath, message.Scene, ct).ConfigureAwait(false);

        if (
            !await renderer
                .RenderAsync(
                    paths.RenderDataPath,
                    paths.PicturePath,
                    ct,
                    frame: FRAME,
                    scenePath: hasScene ? paths.ScenePath : null
                )
                .ConfigureAwait(false)
        )
            return false;

        return await directory.PictureTakenAsync(roomId, playerId, ct).ConfigureAwait(false);
    }
}
