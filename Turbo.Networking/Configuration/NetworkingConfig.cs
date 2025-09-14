namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

    public required NetworkServerConfig TcpServer { get; init; }
    public required NetworkIncomingQueueConfig IncomingQueue { get; init; }
    public required NetworkEncryptionConfig Encryption { get; init; }
}
