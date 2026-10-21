using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Habbo's external variables as a source of updates, as its texts are: what a version a check
/// kept has that the hotel's variables don't - keys added, changed, or gone from Habbo - and taking
/// the keys staff pick in. Keys the hotel owns are never offered: those that follow a setting or a
/// file, those it keeps itself, the reception's, and those its ignore list or an address names.
/// </summary>
public interface IHabboVariableService
{
    /// <summary>
    /// What taking the version in (the newest when no id is given) would change; null when there
    /// is no such version.
    /// </summary>
    public Task<HabboVariableUpdatePreview?> PreviewAsync(int? versionId, CancellationToken ct);

    /// <summary>
    /// Takes the keys named in from the version, or every key waiting when none are named; a key
    /// that isn't waiting is left. Null when nothing was taken in; otherwise the change set made,
    /// which rolls back as one. Throws <see cref="System.ArgumentException"/> when there is no such
    /// version.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> TakeAsync(
        int versionId,
        IReadOnlyCollection<string>? keys,
        PlayerId player,
        CancellationToken ct
    );
}
