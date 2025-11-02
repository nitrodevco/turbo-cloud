using System.Threading.Tasks;
using Orleans;
using Turbo.Networking.Abstractions.Session;
using Turbo.Players.Abstractions;

namespace Turbo.Players.Grains;

public class PlayerEndpointGrain(ISessionGateway sessionGateway) : Grain, IPlayerEndpointGrain
{
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    private string? _connectionId;

    public Task BindConnectionAsync(string connectionId)
    {
        _connectionId = connectionId;

        return Task.CompletedTask;
    }
}
