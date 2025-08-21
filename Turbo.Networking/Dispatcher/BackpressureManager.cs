using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Dispatcher;

public class BackpressureManager(IEmulatorConfig config) : IBackpressureManager
{
    private bool _readsPaused;
    private readonly object _stateLock = new();

    public int PauseReadsThreshold { get; } =
        config.Network.DispatcherOptions.PauseReadsThresholdGlobal;

    public int ResumeReadsThreshold { get; } =
        config.Network.DispatcherOptions.ResumeReadsThresholdGlobal;

    public void UpdateDepth(int depth, ISessionManager sessionManager)
    {
        lock (_stateLock)
        {
            // Only act when crossing thresholds and state changes are needed
            if (depth > PauseReadsThreshold && !_readsPaused)
            {
                sessionManager.PauseReadsOnAll();
                _readsPaused = true;
            }
            else if (depth < ResumeReadsThreshold && _readsPaused)
            {
                sessionManager.ResumeReadsOnAll();
                _readsPaused = false;
            }
        }
    }
}
