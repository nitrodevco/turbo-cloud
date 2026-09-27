using System;

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

    /// <summary>
    /// Encrypts or decrypts <paramref name="data"/> where it lies, advancing the key stream.
    /// The per-packet paths use this so a packet is not copied just to be transformed.
    /// </summary>
    public void ProcessInPlace(Span<byte> data);

    public byte[] Peek(byte[] inputData, int inputOffset = 0, int? length = null);
    public void Peek(
        byte[] inputData,
        int inputOffset,
        byte[] outputData,
        int outputOffset,
        int length
    );

    /// <summary>
    /// Transforms <paramref name="input"/> into <paramref name="output"/> without advancing the
    /// key stream. The two may be the same span.
    /// </summary>
    public void Peek(ReadOnlySpan<byte> input, Span<byte> output);
}
