namespace Turbo.Core.Networking.Encryption;

public interface IRc4Service
{
    byte[] ProcessBytes(byte[] bytes);
}
