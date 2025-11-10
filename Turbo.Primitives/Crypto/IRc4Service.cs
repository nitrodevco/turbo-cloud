namespace Turbo.Primitives.Crypto;

public interface IRc4Service
{
    public byte[] Process(byte[] inputData, byte[]? outputData = null, int? inputOffset = 0);
}
