namespace Turbo.Primitives.Quests.Enums;

/// <summary>
/// How the client draws a reward: the product type id its <c>ProductIconWidget</c> switches on
/// (AS3 <c>ProductIconWidget</c>, <c>productTypeId - -1</c>), with the item type id it reads
/// beside it. Only the members that switch names are listed.
/// </summary>
public enum ProductDisplayType : short
{
    Unknown = -1,

    /// <summary>A wall furni; the item type id is its definition sprite id.</summary>
    WallItem = 0,

    /// <summary>A floor furni; the item type id is its definition sprite id.</summary>
    FloorItem = 1,

    /// <summary>An avatar effect; the item type id is the effect id.</summary>
    Effect = 2,

    /// <summary>A badge; the item type id is the badge code.</summary>
    Badge = 4,

    /// <summary>A bot; the extra parameters are its figure.</summary>
    Bot = 6,

    /// <summary>Activity points; the item type id is the point type (0 is duckets).</summary>
    ActivityPoints = 8,

    /// <summary>A chat bubble style; the item type id is the style id.</summary>
    ChatStyle = 9,

    /// <summary>A pet; the extra parameters are its figure.</summary>
    Pet = 10,

    /// <summary>A Habbicon; the item type id is its id.</summary>
    Habbicon = 12,
}
