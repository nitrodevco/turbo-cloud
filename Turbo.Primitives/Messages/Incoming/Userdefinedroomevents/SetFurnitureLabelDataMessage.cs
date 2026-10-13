using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// The text of a label furni, sent when its bubble closes or loses focus with changed text
/// (Flash <c>SetFurnitureLabelDataMessageComposer</c>: int furni, string text;
/// <c>FurnitureLabelView.saveIfChanged</c>).
/// </summary>
public record SetFurnitureLabelDataMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string Text { get; init; }
}
