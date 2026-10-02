using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Players.Messenger;

/// <summary>
/// Everything a friend list or messenger packet asks of the server, one method per packet. The
/// packet handlers only check who is asking and send back what a method returns: every lookup,
/// grain call and choice of reply lives here, and the state behind it in the player's messenger
/// grain. A method that returns a composer returns the reply for the asking session, or null
/// when the client is owed none; one that returns nothing answers through the grains.
/// </summary>
public interface IMessengerService
{
    /// <summary>Sends the messenger's first load (<c>MessengerInit</c>).</summary>
    public Task SendInitAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>The requests waiting for the player (<c>GetFriendRequests</c>).</summary>
    public Task<IComposer> GetFriendRequestsAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Friend list changes not pushed yet (<c>FriendListUpdate</c>, which the client asks for on
    /// a timer); null when there are none.
    /// </summary>
    public Task<IComposer?> GetFriendListUpdateAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Asks a player by name to be a friend; the refusal to show, or null.</summary>
    public Task<IComposer?> RequestFriendAsync(
        PlayerId playerId,
        string targetName,
        CancellationToken ct
    );

    /// <summary>Accepts requests by requester id; the failures to show, or null.</summary>
    public Task<IComposer?> AcceptFriendRequestsAsync(
        PlayerId playerId,
        List<int> requesterIds,
        CancellationToken ct
    );

    /// <summary>Declines requests, or all of them. The client clears its own list.</summary>
    public Task DeclineFriendRequestsAsync(
        PlayerId playerId,
        List<PlayerId> requesterIds,
        bool declineAll,
        CancellationToken ct
    );

    /// <summary>Ends friendships; both sides are told through their messengers.</summary>
    public Task RemoveFriendsAsync(
        PlayerId playerId,
        List<PlayerId> friendIds,
        CancellationToken ct
    );

    /// <summary>Sets what a friend is to the player; the friend list update carries it back.</summary>
    public Task SetRelationshipStatusAsync(
        PlayerId playerId,
        PlayerId friendId,
        int relationType,
        CancellationToken ct
    );

    /// <summary>The players whose names start with the query, split into friends and others.</summary>
    public Task<IComposer> SearchAsync(PlayerId playerId, string query, CancellationToken ct);

    /// <summary>
    /// Sends a console message to a friend, or to a group chat (a negative chat id); the error
    /// to show in the conversation, or null.
    /// </summary>
    public Task<IComposer?> SendMessageAsync(
        PlayerId playerId,
        int chatId,
        string message,
        int confirmationId,
        CancellationToken ct
    );

    /// <summary>A page of a conversation's stored history before a cursor, or null for none.</summary>
    public Task<IComposer?> GetMessageHistoryAsync(
        PlayerId playerId,
        int chatId,
        string beforeMessageId,
        CancellationToken ct
    );

    /// <summary>Invites friends to the player's room; the recipients not reached, or null.</summary>
    public Task<IComposer?> SendRoomInviteAsync(
        PlayerId playerId,
        List<int> friendIds,
        string message,
        CancellationToken ct
    );

    /// <summary>Follows a friend into their room; the reason it could not, or null.</summary>
    public Task<IComposer?> FollowFriendAsync(
        PlayerId playerId,
        PlayerId friendId,
        CancellationToken ct
    );

    /// <summary>Goes to the room a player, found by name, is standing in, if any.</summary>
    public Task VisitUserAsync(PlayerId playerId, string targetName, CancellationToken ct);

    /// <summary>
    /// The friend bar's "find new friends": to a random room with players in it, telling the
    /// player first whether one was found.
    /// </summary>
    public Task FindNewFriendsAsync(PlayerId playerId, CancellationToken ct);
}
