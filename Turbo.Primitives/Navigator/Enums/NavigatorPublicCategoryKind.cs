namespace Turbo.Primitives.Navigator.Enums;

/// <summary>What fills a category of the navigator's Public tab.</summary>
public enum NavigatorPublicCategoryKind
{
    /// <summary>The rooms staff put in it, in the order they set (Habbo's <c>official_&lt;id&gt;</c> blocks).</summary>
    Manual = 0,

    /// <summary>
    /// The rooms with the staff pick flag (the room info's "Add to staff picked rooms"), newest pick
    /// first, as the block <c>staffpicks</c> Habbo's client titles "Staff Picks".
    /// </summary>
    StaffPicks = 1,
}
