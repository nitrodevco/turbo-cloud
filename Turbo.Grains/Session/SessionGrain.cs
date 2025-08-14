using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Core.Contracts.Session;

namespace Turbo.Grains.Session;

public sealed class SessionGrain : Grain, ISessionGrain
{
    private long _playerId;
    private Guid _connectionId;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        //var provider = this.GetStreamProvider(PlayerStreams.Provider);
        //_out = provider.GetStream<IOutboundMessage>(PlayerStreams.Id(long.Parse(this.GetPrimaryKeyString())));
        return Task.CompletedTask;
    }

    public Task Connect(Guid connectionId, long playerId)
    {
        _connectionId = connectionId;
        _playerId = playerId;

        return Task.CompletedTask;
    }

    public Task Disconnect(Guid connectionId, string reason)
    {
        if (connectionId == _connectionId)
        {
            // persist state, mark offline, notify room, etc.
        }
        return Task.CompletedTask;
    }
}