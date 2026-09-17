namespace Turbo.Primitives.Navigator.Enums;

/// <summary>What an entry in the legacy official rooms list carries after its header.</summary>
public enum NavigatorOfficialRoomEntryType
{
    /// <summary>A folder the client opens with a tag search.</summary>
    Tag = 1,

    /// <summary>A room, sent as guest room data.</summary>
    GuestRoom = 2,

    /// <summary>A folder of further entries.</summary>
    Folder = 3,
}
