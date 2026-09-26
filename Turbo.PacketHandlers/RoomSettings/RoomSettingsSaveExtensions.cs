using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// The two halves of saving room settings that live outside the room grain.
///
/// Both the full settings form and the category-and-trade shortcut ask the navigator whether the
/// chosen category is one this player may use — only the navigator knows — and both report a
/// refusal on the same packet.
/// </summary>
internal static class RoomSettingsSaveExtensions
{
    /// <summary>
    /// The category the player asked for when it is one they may list a room in, otherwise null,
    /// which the room grain reads as "leave the category as it is".
    /// </summary>
    public static int? ResolvePlayerFlatCategory(
        this INavigatorService navigatorService,
        PlayerId playerId,
        int categoryId
    ) =>
        navigatorService.GetFlatCategoriesForPlayer(playerId).Any(x => x.Id == categoryId)
            ? categoryId
            : null;

    /// <summary>
    /// Sends the refusal the result carries, or nothing when it succeeded. Returns whether the
    /// save went through, so a caller that confirms success can do so.
    /// </summary>
    public static async Task<bool> SendRoomSettingsSaveFailureAsync(
        this MessageContext ctx,
        RoomId roomId,
        RoomSettingsSaveResultSnapshot result,
        CancellationToken ct
    )
    {
        if (result.Succeeded)
            return true;

        await ctx.SendComposerAsync(
                new RoomSettingsSaveErrorEventMessageComposer
                {
                    RoomId = roomId,
                    Error = result.Error,
                    Info = result.Info,
                },
                ct
            )
            .ConfigureAwait(false);

        return false;
    }
}
