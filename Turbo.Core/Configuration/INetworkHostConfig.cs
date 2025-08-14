namespace Turbo.Core.Configuration;

public class INetworkHostConfig
{
    public NetworkHostType Type { get; init; }
    public bool Enabled { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
}