namespace Turbo.Networking.Configuration;

public class NetworkEncryptionConfig
{
    public required string KeySize { get; init; }
    public required string PublicKey { get; init; }
    public required string PrivateKey { get; init; }
}
