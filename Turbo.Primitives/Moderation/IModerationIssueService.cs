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

    /// <summary>Reports deleted by their reporter: gone from every moderator's queue (<c>IssueDeleted</c>).</summary>
    Task RemovedAsync(ImmutableArray<int> issueIds, CancellationToken ct);

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

    /// <summary>
    /// The issue handler's "close with default sanction" (<c>IssueManager.closeDefaultAction</c>):
    /// the moderator's picked issues close as resolved and the reported player gets their next
    /// default sanction (<see cref="GetDefaultSanctionAsync"/>), recorded on the first issue.
    /// </summary>
    Task CloseWithDefaultSanctionAsync(
        PlayerId moderator,
        ImmutableArray<int> issueIds,
        CancellationToken ct
    );

    /// <summary>
    /// The mod action window's "Default sanction" (<c>ModActionCtrl.onDefaultSanctionButton</c>):
    /// the player gets the default sanction its button showed (<see cref="GetDefaultSanctionAsync"/>),
    /// told <paramref name="message"/>, or the topic when the moderator wrote nothing. Taken from an
    /// issue the moderator holds about that player, it is recorded on the issue, which closes as
    /// resolved. False when there is no default sanction or it was refused.
    /// </summary>
    Task<bool> GiveDefaultSanctionAsync(
        PlayerId moderator,
        PlayerId player,
        int topicId,
        string message,
        int issueId,
        CancellationToken ct
    );

    /// <summary>
    /// The default sanction the player would get next, as <c>CfhSanction</c> shows it in the issue
    /// handler (<c>IssueManager.updateSanctionData</c>); null when the hotel configures none.
    /// </summary>
    Task<SanctionTypeSnapshot?> GetDefaultSanctionAsync(PlayerId player, CancellationToken ct);

    /// <summary>The player an issue reports; null for a report about no one or no such issue.</summary>
    Task<PlayerId?> GetReportedPlayerAsync(int issueId, CancellationToken ct);

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
