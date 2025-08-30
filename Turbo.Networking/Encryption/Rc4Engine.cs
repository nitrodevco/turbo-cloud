using System;
using Turbo.Core.Networking.Encryption;

namespace Turbo.Networking.Encryption;

public class Rc4Engine : IStreamCipher
{
    private readonly byte[] _engineState = new byte[256]; // S
    private int _x; // i
    private int _y; // j
    private byte[]? _workingKey;

    public Rc4Engine(ICipherParameters parameters)
    {
        if (parameters is not KeyParameter kp)
            throw new ArgumentException("Rc4Engine requires KeyParameter", nameof(parameters));

        _workingKey = (byte[])kp.Key.Clone();
        SetKey(_workingKey);
    }

    public string AlgorithmName => "RC4";

    public byte[] ProcessBytes(byte[] bytes, byte[] output = null)
    {
        output ??= new byte[bytes.Length];

        ProcessBytes(bytes, 0, bytes.Length, output, 0);

        return output;
    }

    public void ProcessBytes(byte[] input, int inOff, int length, byte[] output, int outOff)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        if (
            inOff < 0
            || outOff < 0
            || length < 0
            || inOff + length > input.Length
            || outOff + length > output.Length
        )
            throw new ArgumentOutOfRangeException("Invalid in/out offsets or length.");

        for (int i = 0; i < length; i++)
            output[outOff + i] = (byte)(input[inOff + i] ^ NextKeyByte());
    }

    public byte ReturnByte(byte input) => (byte)(input ^ NextKeyByte());

    public void Reset()
    {
        if (_workingKey is null)
            return;
        SetKey(_workingKey);
    }

    private void SetKey(byte[] key)
    {
        // KSA
        for (int i = 0; i < 256; i++)
            _engineState[i] = (byte)i;

        _x = 0;
        _y = 0;

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + _engineState[i] + key[i % key.Length]) & 0xFF;
            // swap S[i], S[j]
            (_engineState[i], _engineState[j]) = (_engineState[j], _engineState[i]);
        }
    }

    private byte NextKeyByte()
    {
        // PRGA
        _x = (_x + 1) & 0xFF;
        _y = (_y + _engineState[_x]) & 0xFF;
        (_engineState[_x], _engineState[_y]) = (_engineState[_y], _engineState[_x]);
        int idx = (_engineState[_x] + _engineState[_y]) & 0xFF;
        return _engineState[idx];
    }
}
