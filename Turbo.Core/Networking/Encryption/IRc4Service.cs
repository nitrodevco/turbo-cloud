namespace Turbo.Core.Networking.Encryption;

public interface IRc4Service
{
    void SetKey(byte[] key);
    byte[] Encrypt(byte[] inputData);
    byte[] Decrypt(byte[] inputData);
}