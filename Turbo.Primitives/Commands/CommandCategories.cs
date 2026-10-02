namespace Turbo.Primitives.Commands;

/// <summary>The sections core's own commands are listed under. A plugin may use any other name.</summary>
public static class CommandCategories
{
    /// <summary>What a command that names no category is listed under, and always first.</summary>
    public const string GENERAL = "General";

    public const string MODERATION = "Moderation";

    /// <summary>Telling players something: an alert to a player, a room or the hotel.</summary>
    public const string ANNOUNCEMENTS = "Announcements";

    /// <summary>Looking after a player: finding them, and putting right what went wrong for them.</summary>
    public const string SUPPORT = "Support";

    /// <summary>Running the hotel itself: access, status, maintenance, reloads.</summary>
    public const string ADMINISTRATION = "Administration";
}
