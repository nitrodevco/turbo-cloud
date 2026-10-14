using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players;

/// <summary>Who may speak with which chat bubble.</summary>
public static class ChatStyles
{
    /// <summary>The plain bubble: every player may use it, whatever the table says about it.</summary>
    public const int DEFAULT_STYLE_ID = 0;

    /// <summary>
    /// Whether the client counts a style id as an NFT bubble, offered only to an owner named in
    /// UserNftChatStyles (<c>RoomChatInputView.isNftChatStyle</c>: 1000 to 9999).
    /// </summary>
    public static bool IsNftStyle(int clientStyleId) => clientStyleId is >= 1000 and <= 9999;

    /// <summary>
    /// Whether the client counts a style id as a purchasable bubble, offered only to an owner
    /// named in UserPurchasableChatStyles (<c>RoomChatInputView.isPurchasableStyle</c>: 10000 to
    /// 99999, shown when <c>custom.chat.styles.enabled</c>).
    /// </summary>
    public static bool IsPurchasableStyle(int clientStyleId) =>
        clientStyleId is >= 10000 and <= 99999;

    /// <summary>
    /// Whether a player may speak with <paramref name="style"/>. Every flag the style carries has
    /// to be met. Staff meet the ambassador flag as well as their own, as the client offers
    /// ambassador bubbles to both; a system bubble is never a player's.
    /// </summary>
    public static bool CanSpeakWith(
        ChatStyleSnapshot style,
        bool hasClub,
        bool isAmbassador,
        bool isStaff,
        bool ownsStyle
    )
    {
        if (style.System)
            return false;

        if (style.StaffOnly && !isStaff)
            return false;

        if (style.AmbassadorOnly && !(isAmbassador || isStaff))
            return false;

        if (style.ClubOnly && !hasClub)
            return false;

        if (style.Purchasable && !ownsStyle)
            return false;

        return true;
    }
}
