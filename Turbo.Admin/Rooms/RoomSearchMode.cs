namespace Turbo.Admin.Rooms;

/// <summary>What a room search matches its text against.</summary>
public enum RoomSearchMode
{
    /// <summary>Rooms whose name contains the text.</summary>
    Name,

    /// <summary>Rooms whose owner's name starts with the text.</summary>
    Owner,

    /// <summary>The room with that id.</summary>
    Id,

    /// <summary>
    /// Rooms whose owner has that id: one player's rooms, where a name would also match the
    /// names that start like it.
    /// </summary>
    OwnerId,
}
