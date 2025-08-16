namespace Turbo.Networking.Extensions;

using DotNetty.Buffers;

public static class ByteBufferExtensions
{
    public static bool ReleaseAll(this IByteBuffer buffer)
    {
        return buffer.ReferenceCount != 0 && buffer.Release(buffer.ReferenceCount);
    }
}
