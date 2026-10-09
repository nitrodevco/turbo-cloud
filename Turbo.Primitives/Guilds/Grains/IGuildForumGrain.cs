using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Guilds.Grains;

/// <summary>
/// A group's forum: its settings, threads and messages. Every request names the player making
/// it, whose rank in the group (and staff permission) decides what they may see and do; each
/// answer is sent to that player.
/// </summary>
public interface IGuildForumGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// Opens the group's forum when its owner bought a forum terminal for it ("In order to start a
    /// group forum the group owner must first purchase a forum terminal for the group"). True when
    /// a forum was opened; false when the buyer is not the owner or there already is one.
    /// </summary>
    Task<bool> OpenAsync(PlayerId buyer, CancellationToken ct);

    Task SendForumAsync(PlayerId viewer, CancellationToken ct);

    Task SendThreadsAsync(PlayerId viewer, int startIndex, int amount, CancellationToken ct);

    Task SendThreadAsync(PlayerId viewer, int threadId, CancellationToken ct);

    Task SendMessagesAsync(
        PlayerId viewer,
        int threadId,
        int startIndex,
        int amount,
        CancellationToken ct
    );

    /// <summary>Starts a thread (<paramref name="threadId"/> 0) or replies to one.</summary>
    Task PostAsync(
        PlayerId author,
        int threadId,
        string subject,
        string text,
        CancellationToken ct
    );

    Task ModerateThreadAsync(PlayerId actor, int threadId, int state, CancellationToken ct);

    Task ModerateMessageAsync(
        PlayerId actor,
        int threadId,
        int messageId,
        int state,
        CancellationToken ct
    );

    Task UpdateThreadAsync(
        PlayerId actor,
        int threadId,
        bool isSticky,
        bool isLocked,
        CancellationToken ct
    );

    Task UpdateSettingsAsync(
        PlayerId actor,
        int readPermission,
        int postMessagePermission,
        int postThreadPermission,
        int moderatePermission,
        CancellationToken ct
    );
}
