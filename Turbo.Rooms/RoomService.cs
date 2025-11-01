using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Grains;
using Turbo.Rooms.Abstractions;

namespace Turbo.Rooms;

public sealed class RoomService(ILogger<IRoomService> logger, IGrainFactory grainFactory)
    : IRoomService
{
    private readonly ILogger<IRoomService> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<IRoomGrain> GetRoomGrainAsync(long roomId)
    {
        var grain = _grainFactory.GetGrain<IRoomGrain>(roomId);

        return await Task.FromResult(grain).ConfigureAwait(false);
    }
}
