namespace Turbo.Guilds.Configuration;

/// <summary>
/// What the hotel decides about groups. Every limit the client shows a message for is here
/// rather than in a grain, because the message the player reads is the hotel's to set: the
/// client's texts name 50 and 400 memberships and 100 owned groups, so a hotel that moves these
/// should move its texts with them.
/// </summary>
public class GuildConfig
{
    public const string SECTION_NAME = "Turbo:Guilds";

    /// <summary>What creating a group costs, sent to the client before it commits.</summary>
    public int CreationCostInCredits { get; init; } = 10;

    /// <summary>The editor stops typing here; the server clamps rather than refusing.</summary>
    public int NameMaxLength { get; init; } = 30;
    public int DescriptionMaxLength { get; init; } = 255;

    /// <summary>Groups one player may belong to without a Habbo Club membership.</summary>
    public int MembershipsMax { get; init; } = 50;

    /// <summary>Groups a Habbo Club member may belong to.</summary>
    public int MembershipsMaxWithClub { get; init; } = 400;

    /// <summary>Groups one player may own.</summary>
    public int OwnedGuildsMax { get; init; } = 100;

    /// <summary>Members a <c>Regular</c> group takes before it is full.</summary>
    public int RegularGuildMembersMax { get; init; } = 5000;

    /// <summary>Whether creating a group needs a Habbo Club membership.</summary>
    public bool CreationRequiresClub { get; init; }

    /// <summary>
    /// Rows per page of the member window. The server tells the client its own page size, so
    /// this is the only place it is decided.
    /// </summary>
    public int MembersPageSize { get; init; } = 14;

    /// <summary>Mirrors the client's <c>group.deletion.enabled</c>.</summary>
    public bool DeletionEnabled { get; init; } = true;

    /// <summary>
    /// A group larger than this cannot be deleted. Mirrors the client's
    /// <c>group.deletion.maximum.members</c>, which only greys out the button.
    /// </summary>
    public int DeletionMaxMembers { get; init; } = 500;

    /// <summary>Mirrors the client's <c>group.blocking.enabled</c>.</summary>
    public bool BlockingEnabled { get; init; } = true;

    /// <summary>Guild base rooms the navigator's hottest-groups search returns.</summary>
    public int GuildBaseSearchResultLimit { get; init; } = 50;

    /// <summary>Groups a name search returns.</summary>
    public int NameSearchResultLimit { get; init; } = 50;

    /// <summary>
    /// How often the directory re-reads every group from the database. Changes made through the
    /// group grains reach it at once; this is what catches a row edited behind the server's
    /// back, and what a silo starting mid-life rebuilds from.
    /// </summary>
    public int DirectoryRefreshMs { get; init; } = 300000;
}
