namespace Turbo.Core.Authorization.Encryption;

public interface IRc4Service
{
    void SetKey(byte[] key);
    byte[] Encrypt(byte[] inputData);
    byte[] Decrypt(byte[] inputData);
}
