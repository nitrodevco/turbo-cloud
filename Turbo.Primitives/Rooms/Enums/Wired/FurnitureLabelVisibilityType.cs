namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Who sees a furniture label (Flash <c>FurnitureLabelData.VISIBILITY_*</c>, the settings window's
/// "Who can see this label?"). The client decides: <c>FurnitureLabelWidget.canView</c> shows a
/// label to everyone only at <see cref="Everyone"/>, otherwise to players with wired read rights.
/// </summary>
public enum FurnitureLabelVisibilityType
{
    Everyone = 0,

    /// <summary>"Users with Wired inspection rights": the room's wired read permission.</summary>
    WiredInspectors = 1,
}
