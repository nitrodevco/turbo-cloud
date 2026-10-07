namespace Turbo.Assets;

/// <summary>
/// The most an asset file may unpack to. A file's own header says what it unpacks to, and a file
/// from anywhere could say anything: past this it is refused before anything is inflated.
/// </summary>
public static class AssetLimits
{
    public const int MAX_INFLATED_BYTES = 256 * 1024 * 1024;

    public static void CheckInflatedSize(long size, string what)
    {
        if (size < 0 || size > MAX_INFLATED_BYTES)
            throw new AssetFormatException(
                $"{what} says it unpacks to {size} bytes, more than the {MAX_INFLATED_BYTES} allowed."
            );
    }
}
