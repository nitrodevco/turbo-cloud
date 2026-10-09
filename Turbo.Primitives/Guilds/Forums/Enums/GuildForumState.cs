namespace Turbo.Primitives.Guilds.Forums.Enums;

/// <summary>
/// A thread's or a message's moderation state, the byte the client reads. Hiding sends 10 (a
/// forum moderator) or 20 (staff), restoring sends 1 (AS3 GroupForumController.deleteThread,
/// unDeleteThread, deleteMessage, unDeleteMessage); the views mask anything above 1 for a viewer
/// who cannot moderate, and 20 for anyone but staff.
/// </summary>
public enum GuildForumState : byte
{
    Normal = 0,
    Restored = 1,
    HiddenByAdmin = 10,
    HiddenByStaff = 20,
}
