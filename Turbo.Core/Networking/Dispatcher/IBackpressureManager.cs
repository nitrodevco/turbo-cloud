using Turbo.Core.Networking.Session;

namespace Turbo.Core.Networking.Dispatcher;

public interface IBackpressureManager
{
    void UpdateDepth(int depth, ISessionManager sessionManager);
}
