using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetOnlineStatusAsync(bool flag, CancellationToken ct);
    public Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct);
    public Task SetMottoAsync(string text, CancellationToken ct);
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    );
}
