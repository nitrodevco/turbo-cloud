namespace Turbo.Primitives.Habbicons.Enums;

/// <summary>
/// A Habbicon's state for one player, as the client reads it (AS3 <c>HabbiconState</c>,
/// <c>HabbiconView.createEntryFromData</c>). <see cref="Claimable"/>, <see cref="Owned"/> and
/// <see cref="Favourite"/> are the player's own (<c>HabbiconController.isStoredUserState</c>);
/// the others describe a Habbicon the player does not have.
/// </summary>
public enum HabbiconState
{
    /// <summary>Not owned; bought when it has a price (<c>purchasable = state == 0 &amp;&amp; price</c>).</summary>
    Available = 0,

    /// <summary>Earned, waiting for the player to claim it (a completed collection's reward).</summary>
    Claimable = 1,

    Owned = 2,

    /// <summary>Owned and in the player's favourites (<c>favorite = state == 3</c>).</summary>
    Favourite = 3,

    /// <summary>A collection's reward the player has not earned yet (recorded on every reward of an unfinished collection).</summary>
    Locked = 4,

    /// <summary>Not sold: given by something else, such as a reward track (recorded on every unpriced Habbicon).</summary>
    Reward = 5,
}
