using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Players.Grains.Messenger;

public interface IPlayerMessengerGrain : IGrainWithIntegerKey
{
    // The tells: what one messenger calls on another. Interleaved and memory-only, because two
    // friends acting on each other at once would otherwise leave both grains waiting.

    /// <summary>Whether <paramref name="playerId"/> may add this player: limit and block list.</summary>
    [AlwaysInterleave]
    public Task<FriendListErrorCodeType> CanBeAddedByAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>The adding side already wrote the friendship; this side only shows it.</summary>
    [AlwaysInterleave]
    public Task OnFriendAddedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);

    /// <summary>The removing side already deleted the friendship; this side only shows it.</summary>
    [AlwaysInterleave]
    public Task OnFriendRemovedAsync(PlayerId playerId, CancellationToken ct);

    public Task RemoveFriendsAsync(List<PlayerId> playerIds, CancellationToken ct);
    public Task<List<MessengerAcceptFriendFailure>> AcceptFriendRequestsAsync(
        List<int> playerIds,
        CancellationToken ct
    );
    public Task DeclineFriendRequestsAsync(
        List<PlayerId> playerIds,
        bool declineAll,
        CancellationToken ct
    );
    public Task<MessengerRequestFriendResult> SendFriendRequestAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task<MessengerRequestFriendResult> ReceieveFriendRequestAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    );
    public Task BlockPlayerAsync(PlayerId targetId, CancellationToken ct);
    public Task UnblockPlayerAsync(PlayerId playerId, CancellationToken ct);
    public Task<MessengerIgnoreResultType> IgnorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    );
    public Task<MessengerIgnoreResultType> UnignorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    );
    public Task UpdateFriendsAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);

    [AlwaysInterleave]
    public Task RecieveFriendUpdateAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
    public Task<bool> SetRelationshipStatusAsync(
        PlayerId friendId,
        MessengerFriendRelationType status,
        CancellationToken ct
    );
    public Task<(
        List<MessengerSearchResultSnapshot> Friends,
        List<MessengerSearchResultSnapshot> Others
    )> SearchPlayersAsync(string query, CancellationToken ct);

    /// <summary>
    /// Why this player would refuse a console message from <paramref name="senderId"/>, or null
    /// when they would take it. Interleaved and memory-only: the sender asks before it stores the
    /// message, and two friends messaging each other at once must not wait on each other.
    /// </summary>
    [AlwaysInterleave]
    public Task<InstantMessageErrorCodeType?> CanReceiveMessageAsync(
        PlayerId senderId,
        CancellationToken ct
    );

    /// <summary>
    /// Stores a console message to a friend and delivers it: at once when the friend is online,
    /// or when their messenger next starts (<see cref="SendInitAsync"/>). The sender's own copy
    /// confirms the message only once it is stored and accepted. Returns the refusal the sender
    /// is shown instead, or null.
    /// </summary>
    public Task<InstantMessageErrorCodeType?> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    );

    /// <summary>
    /// The sender's stored message <paramref name="messageId"/>, for the owner's open session.
    /// Checks the friendship again, since it may have ended since the sender asked, and returns
    /// the refusal when it did; otherwise shows the message and marks the row delivered.
    /// </summary>
    [AlwaysInterleave]
    public Task<InstantMessageErrorCodeType?> ReceiveMessageAsync(
        PlayerId senderId,
        string message,
        DateTime sentAtUtc,
        int messageId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    );

    /// <summary>
    /// A page of the stored conversation with the friend <paramref name="chatPartnerId"/>, oldest
    /// first, of the messages before <paramref name="beforeMessageId"/> (the newest page when it
    /// is empty). Empty while history is off (<c>MessengerHistoryPageSize</c> 0, the default),
    /// for someone who is not a friend, and for a cursor outside this conversation.
    /// </summary>
    public Task<List<MessageHistoryEntrySnapshot>> GetMessageHistoryAsync(
        PlayerId chatPartnerId,
        string beforeMessageId,
        CancellationToken ct
    );

    /// <summary>
    /// Invites friends to the owner's room with a text (<c>RoomInviteView.sendMsg</c>). Returns
    /// the recipients it could not reach: not a friend, offline, or past the recipient limit.
    /// A friend who ignores room invitations is not reported, as the client never learns that.
    /// </summary>
    public Task<List<PlayerId>> SendRoomInviteAsync(
        List<PlayerId> recipientIds,
        string message,
        CancellationToken ct
    );

    /// <summary>
    /// Shows the owner a room invitation from <paramref name="senderId"/> when they are still
    /// friends and not blocked. Interleaved and memory-only, like <see cref="ReceiveMessageAsync"/>.
    /// </summary>
    [AlwaysInterleave]
    public Task ReceiveRoomInviteAsync(PlayerId senderId, string message, CancellationToken ct);

    /// <summary>
    /// Tells the player's online friends something they did, for their friend bar
    /// (`FriendNotificationMessage`, which `HabboFriendBarData.onFriendNotification` turns into a
    /// token on the player's tab): a room event started, an achievement earned, and so on.
    /// </summary>
    public Task NotifyFriendsAsync(
        FriendNotificationCodeType typeCode,
        string message,
        CancellationToken ct
    );

    /// <summary>
    /// Sends the player the messenger's first load: the limits and categories, the friend list in
    /// fragments, then the messages friends sent while they were offline, as one batch.
    /// </summary>
    public Task SendInitAsync(CancellationToken ct);

    /// <summary>
    /// The owner joined or left a group, or one was deleted: the group chats in their friend
    /// list are brought in line with their memberships while they are online. Told by the
    /// player's guild grain; nothing awaits it.
    /// </summary>
    public Task OnGuildMembershipsChangedAsync(CancellationToken ct);

    public Task<List<MessengerCategoryDto>> GetCategoriesAsync(CancellationToken ct);
    public Task<List<MessengerFriendDto>> GetFriendsAsync(CancellationToken ct);

    [AlwaysInterleave]
    public Task<bool> IsFriendAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Whether this player has blocked that one (a gift they would refuse). Interleaved and
    /// memory-only, as <see cref="IsFriendAsync"/>.
    /// </summary>
    [AlwaysInterleave]
    public Task<bool> IsBlockingAsync(PlayerId playerId, CancellationToken ct);
    public Task<List<MessengerRequestDto>> GetRequestsAsync(CancellationToken ct);
    public Task<List<PlayerId>> GetIgnoredAsync(CancellationToken ct);
    public Task<List<MessengerUpdateSnapshot>> GetPendingUpdatesAsync(CancellationToken ct);

    /// <summary>
    /// How many friends this player has. Interleaved and memory-only: the player grain's
    /// profile readers (the LTD raffle) may ask while this grain waits on the player grain.
    /// </summary>
    [AlwaysInterleave]
    public Task<int> GetFriendCountAsync(CancellationToken ct);

    /// <summary>
    /// The friends-list part of this player's profile as <paramref name="viewerId"/> sees it.
    /// Interleaved and memory-only, like <see cref="GetFriendCountAsync"/>.
    /// </summary>
    [AlwaysInterleave]
    public Task<MessengerProfileRelationSnapshot> GetProfileRelationAsync(
        PlayerId viewerId,
        CancellationToken ct
    );

    public Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(
        CancellationToken ct
    );
}
