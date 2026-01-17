using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.Variables;

public readonly record struct WiredVariableKey(WiredVariableTargetType TargetType, string Key)
{
    public override int GetHashCode()
    {
        ulong hashCode = 0;

        hashCode = CombineXor(hashCode, Fnv1a64(Key ?? ""));
        hashCode = CombineXor(hashCode, (ulong)(int)TargetType);

        return (int)unchecked((long)hashCode);
    }

    private static ulong Fnv1a64(string s)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;

        foreach (var ch in s)
        {
            hash ^= (byte)ch;
            hash *= prime;
            hash ^= (byte)(ch >> 8);
            hash *= prime;
        }

        return hash;
    }

    private static ulong CombineXor(ulong hash, ulong value)
    {
        value ^= value >> 33;
        value *= 0xff51afd7ed558ccdUL;
        value ^= value >> 33;
        value *= 0xc4ceb9fe1a85ec53UL;
        value ^= value >> 33;

        return hash ^ value;
    }
}
