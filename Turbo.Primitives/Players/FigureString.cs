namespace Turbo.Primitives.Players;

/// <summary>
/// Shape check for an avatar figure string ("hd-180-1.ch-210-66"): dot separated parts, each a
/// two letter set type followed by dash separated numbers. It says nothing about whether the
/// parts exist or may be worn; it only keeps text that is not a figure at all from being stored
/// and broadcast to a room.
/// </summary>
public static class FigureString
{
    public const int MAX_LENGTH = 512;

    private const int SET_TYPE_LENGTH = 2;

    public static bool IsWellFormed(string? figure)
    {
        if (string.IsNullOrEmpty(figure) || figure.Length > MAX_LENGTH)
            return false;

        foreach (var part in figure.Split('.'))
        {
            var fields = part.Split('-');

            if (fields.Length < 2 || fields[0].Length != SET_TYPE_LENGTH)
                return false;

            foreach (var c in fields[0])
            {
                if (c is < 'a' or > 'z')
                    return false;
            }

            for (var i = 1; i < fields.Length; i++)
            {
                if (fields[i].Length == 0)
                    return false;

                foreach (var c in fields[i])
                {
                    if (c is < '0' or > '9')
                        return false;
                }
            }
        }

        return true;
    }
}
