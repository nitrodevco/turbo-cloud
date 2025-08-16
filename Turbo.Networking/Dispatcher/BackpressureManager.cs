namespace Turbo.Networking.Dispatcher;

using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Session;

public class BackpressureManager(IEmulatorConfig config) : IBackpressureManager
{
    public int PauseReadsThreshold { get; } = config.Network.DispatcherOptions.PauseReadsThresholdGlobal;

    public int ResumeReadsThreshold { get; } = config.Network.DispatcherOptions.ResumeReadsThresholdGlobal;

    public void UpdateDepth(int depth, ISessionManager sessionManager)
    {
        if (depth > PauseReadsThreshold)
        {
            sessionManager.PauseReadsOnAll();
        }
        else if (depth < ResumeReadsThreshold)
        {
            sessionManager.ResumeReadsOnAll();
        }
    }
}
