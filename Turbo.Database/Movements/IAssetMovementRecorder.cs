using System.Collections.Generic;
using Turbo.Database.Context;
using Turbo.Primitives.Movements;

namespace Turbo.Database.Movements;

/// <summary>
/// Adds movements to the originating unit of work, as <c>IAchievementFactRecorder</c> adds facts:
/// the caller's save commits or rolls them back with the change they describe. It does no I/O and
/// never throws: an invalid movement is logged and dropped, and the change still commits. While
/// <see cref="IsRecording"/> is false it adds nothing, so an emit point opens no transaction for it.
/// </summary>
public interface IAssetMovementRecorder
{
    /// <summary><c>Turbo:Movements:Enabled</c>, read now, once its <c>recording.started</c> row is written.</summary>
    bool IsRecording { get; }

    void Record(TurboDbContext db, AssetMovement movement);

    /// <summary>The movements of one save; recording is looked at once for all of them.</summary>
    void Record(TurboDbContext db, IReadOnlyList<AssetMovement> movements);
}
