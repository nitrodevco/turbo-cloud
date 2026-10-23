namespace Turbo.Primitives.Players.Enums.Messenger;

/// <summary>What became of a change to a player's friend list categories.</summary>
public enum MessengerCategoryResultType
{
    Done = 0,

    /// <summary>The hotel does not let players manage their categories (<c>MessengerCategoriesEnabled</c>).</summary>
    Disabled = 1,

    /// <summary>The player has as many categories as <c>MessengerMaxCategories</c> allows.</summary>
    LimitReached = 2,

    /// <summary>The name is empty, too long or not allowed by the word filter.</summary>
    InvalidName = 3,

    /// <summary>The player already has a category by that name.</summary>
    NameTaken = 4,

    /// <summary>No such category of the player's.</summary>
    CategoryNotFound = 5,

    /// <summary>The player is not friends with them.</summary>
    NotFriend = 6,
}
