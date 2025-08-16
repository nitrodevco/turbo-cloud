namespace Turbo.Core.Contracts.Session;

using System;
using System.Threading.Tasks;

using Orleans;

public interface ISessionGrain : IGrainWithStringKey
{
    Task Connect(Guid connectionId, long playerId);

    Task Disconnect(Guid connectionId, string reason);

    // Task Handle(IInboundMessage msg);
}
