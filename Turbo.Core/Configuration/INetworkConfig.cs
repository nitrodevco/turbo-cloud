using System.Collections;
using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public class INetworkConfig
{
    public int NetworkWorkerThreads { get; init; }
    public List<INetworkHostConfig> Hosts { get; init; }
}