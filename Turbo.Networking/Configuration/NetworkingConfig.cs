namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

    public NetworkServerConfig TcpServer { get; init; }
    public NetworkIncomingQueueConfig IncomingQueue { get; init; }
    public NetworkEncryptionConfig Encryption { get; init; }
}
