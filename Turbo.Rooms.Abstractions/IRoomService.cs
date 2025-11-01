using System.Threading.Tasks;
using Turbo.Primitives.Grains;

namespace Turbo.Rooms.Abstractions;

public interface IRoomService
{
    public Task<IRoomGrain> GetRoomGrainAsync(long roomId);
}
