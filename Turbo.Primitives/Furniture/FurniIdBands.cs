namespace Turbo.Primitives.Furniture;

/// <summary>
/// The id range the client reads a furni's kind out of, mirroring its own
/// <c>com.sulake.habbo.utils.FurniId</c>. Nothing else tells it that a furni is lent by the
/// Builders Club: the id alone decides whether the info stand draws it green and titled
/// "The Builders Club Warehouse", whether picking it up warns that it cannot be borrowed again,
/// whether the furni chooser lists its owner as "Builders Club", and whether the pickup
/// animation flies it to the inventory icon.
/// <para>
/// The client splits the whole space three ways. The last two are rules this server keeps:
/// </para>
/// <code>
/// normal          id &lt;= 0x7FFE_BFFF
/// temp (wired)    0x7FFE_C000 .. 0x7FFE_FFFF
/// builders club   0x7FFF_0000 .. 0x7FFF_FFFF
/// </code>
/// <para>
/// Those are the client's own numbers (<c>FurniId.isBuilderClubId</c> is
/// <c>id &gt;= 2147418112</c>). An id below them reads as ordinary furni, so the info stand
/// shows it as the player's own.
/// </para>
/// <para>
/// Temporary furni (placed by wired) take the client's temporary band (<c>FurniId.isTempId</c>,
/// WIN63-202610131318 <c>com/sulake/habbo/utils/_-T1R.as:26</c>): it is all the client reads to
/// draw the info stand blue, put "Temporary Furni" (<c>temp.catalog.title</c>) with the wired icon
/// where the owner goes (<c>InfoStandFurniView.as:574</c>, <c>:597</c>).
/// </para>
/// <para>
/// Builders Club ids are handed out per room, because a furni object id only has to be unique
/// within a room session, and are stored with the row so wired values keyed by furni id keep
/// meaning something across a reload.
/// </para>
/// </summary>
public static class FurniIdBands
{
    public const int BuildersClubMin = 0x7FFF_0000;
    public const int BuildersClubMax = int.MaxValue;

    /// <summary>How many Builders Club furni one room can hold ids for at once.</summary>
    public const int BuildersClubCapacity = BuildersClubMax - BuildersClubMin + 1;

    public static bool IsBuildersClub(int id) => id >= BuildersClubMin;

    public const int TemporaryMin = 0x7FFE_C000;
    public const int TemporaryMax = BuildersClubMin - 1;

    /// <summary>How many temporary furni one room can hold ids for at once.</summary>
    public const int TemporaryCapacity = TemporaryMax - TemporaryMin + 1;

    public static bool IsTemporary(int id) => id is >= TemporaryMin and <= TemporaryMax;
}
