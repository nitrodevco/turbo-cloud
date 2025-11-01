using Microsoft.Extensions.Logging;
using Turbo.Rooms.Abstractions;

namespace Turbo.Rooms;

public sealed class RoomService(ILogger<IRoomService> logger) : IRoomService
{
    private readonly ILogger<IRoomService> _logger = logger;
}
