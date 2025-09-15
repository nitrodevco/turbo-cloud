namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

    public required NetworkEncryptionConfig Encryption { get; init; }
}
