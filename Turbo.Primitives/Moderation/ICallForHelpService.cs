using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Moderation;

/// <summary>Calls for help: what they can be about, the reports players send, and appeals.</summary>
public interface ICallForHelpService
{
    /// <summary>The categories and topics, in their order; read once and held.</summary>
    Task<ImmutableArray<CfhCategorySnapshot>> GetTopicsAsync(CancellationToken ct);

    /// <summary>
    /// Stores a call for help, or refuses it: a topic the hotel does not have, or a reporter
    /// with as many reports waiting as they may have.
    /// </summary>
    Task<CfhResultSnapshot> SubmitAsync(
        PlayerId reporter,
        CfhSubmissionSnapshot submission,
        CancellationToken ct
    );

    /// <summary>The reporter's own reports, newest first.</summary>
    Task<ImmutableArray<CfhReportStatusSnapshot>> GetReportsAsync(
        PlayerId reporter,
        CancellationToken ct
    );

    /// <summary>
    /// Appeals a decision: only the reporter's own report, closed without action and not
    /// appealed yet, which is when the client offers the button. False otherwise.
    /// </summary>
    Task<bool> AppealAsync(PlayerId reporter, int reportId, CancellationToken ct);
}
