using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public interface IEmulatorConfig
{
    public IGameConfig Game { get; init; }

    public INetworkConfig Network { get; init; }

    public bool DatabaseLoggingEnabled { get; init; }

    public List<string> PluginOrder { get; init; }

    public int FloodMessageLimit { get; init; }

    public int FloodTimeFrameSeconds { get; init; }

    public int FloodMuteDurationSeconds { get; init; }
}
