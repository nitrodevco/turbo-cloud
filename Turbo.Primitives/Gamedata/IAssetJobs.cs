using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// What an asset job's work reports through: the step it is on and how many items it has, each
/// item finished, and lines for its log.
/// </summary>
public interface IAssetJobProgress
{
    /// <summary>Starts a step of <paramref name="total"/> items, named <paramref name="phase"/>.</summary>
    void Step(string phase, int total);

    /// <summary>One item of the step finished; <paramref name="failed"/> counts it as failed too.</summary>
    void Advance(bool failed = false);

    void Log(string line);
}

/// <summary>
/// The asset jobs (a sync from Habbo, a publish), one at a time and in the background: the panel
/// asks for <see cref="Current"/> while one runs. Held in memory; a restart forgets the job, and
/// what it finished is kept.
/// </summary>
public interface IAssetJobs
{
    /// <summary>The job running, or the last one; null before the first.</summary>
    AssetJobSnapshot? Current { get; }

    /// <summary>Whether a job is running now.</summary>
    bool Running { get; }

    /// <summary>
    /// Starts <paramref name="work"/> as a job and returns at once. The work returns its result in a
    /// line, or throws to fail; the token it is given is canceled by <see cref="Cancel"/> and when the
    /// server stops. Throws <see cref="InvalidOperationException"/> while another job runs.
    /// </summary>
    AssetJobSnapshot Start(
        AssetJobKind kind,
        string title,
        PlayerId player,
        Func<IAssetJobProgress, CancellationToken, Task<string>> work
    );

    /// <summary>Asks the job running to stop; false when none is.</summary>
    bool Cancel();
}
