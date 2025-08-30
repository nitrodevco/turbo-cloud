namespace Turbo.Core.Networking.Encryption;

public interface IStreamCipher
{
    string AlgorithmName { get; }
    byte[] ProcessBytes(byte[] bytes);
    void ProcessBytes(byte[] input, int inOff, int length, byte[] output, int outOff);
    byte ReturnByte(byte input);
    void Reset();
}
