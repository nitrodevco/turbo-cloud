namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// The room-wide effects the Flash client plays on <c>SpecialRoomEffect</c>
/// (<c>RoomMessageHandler.onSpecialRoomEvent</c>). Each runs for a few seconds on whoever is in the
/// room when it arrives and leaves nothing behind; any other id is ignored.
/// </summary>
public enum SpecialRoomEffectType
{
    /// <summary>The room view turns for five seconds (<c>RoomRotatingEffect</c>).</summary>
    Rotate = 0,

    /// <summary>The room view wobbles for five seconds (<c>RoomShakingEffect</c>).</summary>
    Shake = 1,

    /// <summary>The room view is mirrored, as the client's own <c>:flip</c> does.</summary>
    Flip = 2,

    /// <summary>The room's colour cycles through four disco colours for ten seconds.</summary>
    Disco = 3,
}
