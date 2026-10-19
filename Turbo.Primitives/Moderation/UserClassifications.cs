using System;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// The classes a player is listed under by <c>:uc</c> and <c>:anew</c> (the user classification
/// window, <c>UserClassificationCtrl</c>) and shown under in the moderation tool's user info
/// (<c>UserInfoCtrl</c> <c>user_class_txt</c>). The client prints a class as it is and sends one
/// literal itself, <see cref="NEW"/> (<c>ChatInputWidgetHandler</c> <c>:anew</c>); the others are
/// the server's. Its <c>UserClassificationData</c> names new and paying users, which these follow.
/// A player has one class: new, else paying, else regular.
/// </summary>
public static class UserClassifications
{
    /// <summary>Registered within the hotel's new user window.</summary>
    public const string NEW = "new";

    /// <summary>Past the new user window, with Habbo Club.</summary>
    public const string PAYING = "paying";

    /// <summary>Everyone else.</summary>
    public const string REGULAR = "regular";

    /// <summary>
    /// The class of a player who registered at <paramref name="createdAt"/>: new while less than
    /// <paramref name="newUserAge"/> has passed since, then paying with Habbo Club and regular
    /// without.
    /// </summary>
    public static string Classify(
        DateTime createdAt,
        bool hasClub,
        DateTime now,
        TimeSpan newUserAge
    ) =>
        IsNew(createdAt, now, newUserAge) ? NEW
        : hasClub ? PAYING
        : REGULAR;

    /// <summary>Whether a player who registered at <paramref name="createdAt"/> is still new.</summary>
    public static bool IsNew(DateTime createdAt, DateTime now, TimeSpan newUserAge) =>
        now - createdAt < newUserAge;

    /// <summary>
    /// Whether a list asked for with <paramref name="filter"/> can name anyone: empty asks for every
    /// class, otherwise it has to be one of them, in any case.
    /// </summary>
    public static bool IsFilter(string filter) =>
        filter.Length == 0
        || Matches(filter, NEW)
        || Matches(filter, PAYING)
        || Matches(filter, REGULAR);

    /// <summary>Whether a player of <paramref name="classification"/> is listed for <paramref name="filter"/>.</summary>
    public static bool Matches(string filter, string classification) =>
        filter.Length == 0
        || string.Equals(filter, classification, StringComparison.OrdinalIgnoreCase);
}
