namespace Turbo.Core.Networking.Dispatcher;

using Turbo.Core.Networking.Session;

public interface IBackpressureManager
{
    void UpdateDepth(int depth, ISessionManager sessionManager);
}
