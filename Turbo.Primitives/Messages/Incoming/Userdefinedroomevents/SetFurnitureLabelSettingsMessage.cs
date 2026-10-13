using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// A label's settings window saved (Flash <c>SetFurnitureLabelSettingsMessageComposer</c>: int
/// furni, int colour, int visibility, bool always open; <c>FurnitureLabelSettingsView.save</c>).
/// </summary>
public record SetFurnitureLabelSettingsMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int BubbleColor { get; init; }

    /// <summary>As sent; a value the client never sends is refused by the handler.</summary>
    public required FurnitureLabelVisibilityType Visibility { get; init; }
    public required bool AlwaysOpen { get; init; }
}
