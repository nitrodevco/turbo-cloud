using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Context;
using Turbo.Database.Movements;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Movements;

namespace Turbo.Tests.Support;

/// <summary>
/// The furniture custody as the host builds it, over a test database: recording through the
/// given recorder, or nothing recorded when none is given.
/// </summary>
public static class TestCustody
{
    public static FurnitureCustody Over(
        IDbContextFactory<TurboDbContext> db,
        IAssetMovementRecorder? recorder = null
    ) => new(db, recorder ?? NotRecording.Instance, NullLogger<FurnitureCustody>.Instance);

    /// <summary>A cause for a test that does not look at movements.</summary>
    public static MovementCause Cause() => MovementCause.New("test", 0);

    private sealed class NotRecording : IAssetMovementRecorder
    {
        public static readonly NotRecording Instance = new();

        public bool IsRecording => false;

        public void Record(TurboDbContext db, AssetMovement movement) { }

        public void Record(TurboDbContext db, IReadOnlyList<AssetMovement> movements) { }
    }
}
