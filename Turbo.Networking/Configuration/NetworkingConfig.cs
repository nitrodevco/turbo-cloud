using SuperSocket.Server.Abstractions;

namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

    public int PingIntervalMilliseconds { get; init; } = 10000;
}
