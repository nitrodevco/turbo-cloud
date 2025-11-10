namespace Turbo.Primitives.Crypto;

public interface IRc4Engine
{
    public byte[] Process(byte[] inputData, byte[]? outputData = null, int? inputOffset = 0);
    public byte[] ProcessBytes(
        byte[] inputData,
        int inputOffset,
        int length,
        byte[] outputData,
        int outputOffset
    );
    public byte[] Peek(byte[] inputData, int inputOffset = 0, int? length = null);
    public void Peek(
        byte[] inputData,
        int inputOffset,
        byte[] outputData,
        int outputOffset,
        int length
    );
}
