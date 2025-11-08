using System.Threading.Tasks;
using Orleans;
using Turbo.Networking.Abstractions.Session;
using Turbo.Primitives.Players;

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

    public Task UnbindConnectionAsync(string connectionId)
    {
        if (_connectionId == connectionId)
        {
            _connectionId = null;
        }

        return Task.CompletedTask;
    }
}
