using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// The moderators' call for help queue (1318 AS3 <c>moderation/IssueManager</c>): the reports still
/// open, which a moderator picks, releases or closes. Every change goes to every online holder of
/// <c>moderation.tool</c> as <c>IssueInfo</c>, so all their queues stay the same.
/// </summary>
public interface IModerationIssueService
{
    /// <summary>The reports open or picked, oldest first, as <c>ModeratorInit</c> lists them.</summary>
    Task<ImmutableArray<ModIssueSnapshot>> GetOpenIssuesAsync(CancellationToken ct);

    /// <summary>A new report: sent to every moderator online.</summary>
    Task PublishAsync(int issueId, CancellationToken ct);

    /// <summary>
    /// Gives the moderator the open issues; the ones another moderator holds, or that are closed or
    /// gone, come back as failures. A moderator's own picked issue is not a failure.
    /// </summary>
    Task<ImmutableArray<ModIssuePickFailureSnapshot>> PickAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CancellationToken ct
    );

    /// <summary>Puts the moderator's picked issues back in the queue; others' are left alone.</summary>
    Task ReleaseAsync(PlayerId moderator, ImmutableArray<int> issueIds, CancellationToken ct);

    /// <summary>
    /// Closes the moderator's picked issues for <paramref name="reason"/> and tells each reporter
    /// online (<c>IssueCloseNotification</c>).
    /// </summary>
    Task CloseAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CfhCloseReasonType reason,
        CancellationToken ct
    );

    /// <summary>The chat a report was sent with; null when there is no such report.</summary>
    Task<(int ReporterId, int ReportedId, ModChatlogRecordSnapshot Record)?> GetChatlogAsync(
        int issueId,
        CancellationToken ct
    );

    /// <summary>Where the moderator last left the issue handler; null until they moved it.</summary>
    Task<ModToolPreferencesSnapshot?> GetPreferencesAsync(PlayerId moderator, CancellationToken ct);

    Task SavePreferencesAsync(
        PlayerId moderator,
        ModToolPreferencesSnapshot preferences,
        CancellationToken ct
    );
}
