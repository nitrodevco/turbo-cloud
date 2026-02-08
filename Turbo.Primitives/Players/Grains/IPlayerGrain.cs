using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Grains.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct);
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    );
}
