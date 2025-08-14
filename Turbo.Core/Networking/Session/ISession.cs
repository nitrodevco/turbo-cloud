using System.Threading.Tasks;
using Turbo.Core.Networking.Packets;

namespace Turbo.Core.Networking.Session;

public interface ISession
{
    public Task DisposeAsync();
    public Task Send(IComposer composer);
    public Task SendQueue(IComposer composer);
    public void Flush();
    public void OnMessageReceived(IClientPacket messageEvent);
    public Task HandleDecodedMessages();
}