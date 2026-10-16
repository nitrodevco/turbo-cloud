using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    // The dialog Habbo sends, as recorded.
    private const string TRIGGER_TEXT_NOTICE_TYPE = "wired.error";
    private const string TRIGGER_TEXT_NOTICE_IMAGE = "${image.library.url}album1584/WIRD1.png";
    private const string TRIGGER_TEXT_NOTICE_MESSAGE = "${room.error.wired.trigger_text_capture}";

    /// <summary>
    /// The notice for a player who walks into a room whose wired writes <c>$(trigger_text)</c>
    /// ("WIRED in this room can use the full text of messages that trigger it"); null when no box
    /// does. Habbo sends it right after RoomEntryInfo (official client captures, 18 of 18 entries
    /// into a room whose Show Message held it, builds 1709 and 1318; saving that text without the
    /// placeholder took it away from the next entry and putting it back brought it back). Every
    /// recorded entry was the owner's; that a visitor is told too is inferred from the text.
    /// </summary>
    public NotificationDialogMessageComposer? GetTriggerTextNotice()
    {
        if (
            !_roomGrain._wiredConfig.NotifyTriggerTextCaptureOnEntry
            || !FurniModule.Items.Any(x =>
                x.Logic is FurnitureWiredLogic box && box.UsesTriggerText()
            )
        )
            return null;

        return new NotificationDialogMessageComposer
        {
            NotificationType = TRIGGER_TEXT_NOTICE_TYPE,
            Parameters = ImmutableDictionary<string, string>
                .Empty.Add("image", TRIGGER_TEXT_NOTICE_IMAGE)
                .Add("message", TRIGGER_TEXT_NOTICE_MESSAGE),
        };
    }
}
