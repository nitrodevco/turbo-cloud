using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Tracking;

/// <summary>
/// Keeps what clients report about themselves: their performance, latency and lag warnings, the
/// events they log and the steps of their logins. Every method returns at once; a report is
/// queued and written in batches, and one over the player's rate limit, a full queue or telemetry
/// turned off is dropped, so a report never holds up the session that sent it.
/// </summary>
public interface IClientTelemetry
{
    public void RecordPerformance(
        SessionKey session,
        PlayerId playerId,
        RoomId roomId,
        PerformanceLogMessage report
    );

    public void RecordLatency(
        SessionKey session,
        PlayerId playerId,
        RoomId roomId,
        LatencyPingReportMessage report
    );

    public void RecordLagWarnings(PlayerId playerId, RoomId roomId, LagWarningReportMessage report);

    public void RecordEvent(PlayerId playerId, RoomId roomId, EventLogMessage report);

    /// <summary>
    /// A step of the session's login (<see cref="ClientLoginSteps"/>), timed from the socket
    /// opening. Each step is recorded once per session.
    /// </summary>
    public void RecordLoginStep(SessionKey session, PlayerId playerId, string step);
}
