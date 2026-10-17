using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Habbo's HTML client (the Electron one), as nitro-studio takes it: each environment's
/// <c>/gamedata/clienturls</c> names its release, which is downloaded once, checked against its
/// listing and its own manifest, and kept; the followed environment's is the one the hotel's
/// client libraries are built from.
/// </summary>
public interface IHabboClientService
{
    public Task<HabboClientStatus> GetStatusAsync(CancellationToken ct);

    /// <summary>
    /// Asks every environment for its release and starts downloading one the hotel lacks; with
    /// <paramref name="wait"/>, until those downloads end. Never throws for Habbo's sake: an
    /// environment that did not answer says why in the status.
    /// </summary>
    public Task<HabboClientStatus> CheckAsync(bool wait, CancellationToken ct);

    /// <summary>Forgets a downloaded release; false when there is none of that version, or it is downloading.</summary>
    public Task<bool> ForgetAsync(string version, CancellationToken ct);
}
