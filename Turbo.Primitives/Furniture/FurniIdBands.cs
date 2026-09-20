namespace Turbo.Primitives.Furniture;

/// <summary>
/// The id range the client reads a furni's kind out of, mirroring its own
/// <c>com.sulake.habbo.utils.FurniId</c>. Nothing else tells it that a furni is lent by the
/// Builders Club: the id alone decides whether the info stand draws it green and titled
/// "The Builders Club Warehouse", whether picking it up warns that it cannot be borrowed again,
/// whether the furni chooser lists its owner as "Builders Club", and whether the pickup
/// animation flies it to the inventory icon.
/// <para>
/// The client splits the whole space three ways. Only the last is a rule this server keeps:
/// </para>
/// <code>
/// normal          id &lt;= 0x7FFB_FFFF
/// temp (wired)    0x7FFC_0000 .. 0x7FFD_FFFF
/// builders club   0x7FFE_0000 .. 0x7FFF_FFFF
/// </code>
/// <para>
/// This server's own temporary furni (placed by wired) uses negative ids rather than the
/// client's temporary band, so the client reads them as ordinary furni. That is deliberate:
/// every packet handler refuses an id that is not positive, which is what keeps a player from
/// picking one up or trading it.
/// </para>
/// <para>
/// Builders Club ids are handed out per room, because a furni object id only has to be unique
/// within a room session, and are stored with the row so wired values keyed by furni id keep
/// meaning something across a reload.
/// </para>
/// </summary>
public static class FurniIdBands
{
    public const int BuildersClubMin = 0x7FFE_0000;
    public const int BuildersClubMax = int.MaxValue;

    /// <summary>How many Builders Club furni one room can hold ids for at once.</summary>
    public const int BuildersClubCapacity = BuildersClubMax - BuildersClubMin + 1;

    public static bool IsBuildersClub(int id) => id >= BuildersClubMin;
}
