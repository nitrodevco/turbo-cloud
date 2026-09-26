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
    public Task<bool> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task<bool> ReceiveMessageAsync(
        int chatId,
        string messageText,
        DateTime sentAtUtc,
        string messageId,
        int confirmationId,
        PlayerId senderId,
        string senderName,
        string senderFigure,
        CancellationToken ct,
        int dbMessageId = 0
    );
    public Task<List<MessengerCategoryDto>> GetCategoriesAsync(CancellationToken ct);
    public Task<List<MessengerFriendDto>> GetFriendsAsync(CancellationToken ct);
    public Task<List<MessengerRequestDto>> GetRequestsAsync(CancellationToken ct);
    public Task<List<PlayerId>> GetIgnoredAsync(CancellationToken ct);
    public Task<List<MessengerUpdateSnapshot>> GetPendingUpdatesAsync(CancellationToken ct);
    public Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(
        CancellationToken ct
    );
}
