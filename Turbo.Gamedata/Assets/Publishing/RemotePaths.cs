using System;
using System.Collections.Generic;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>Paths on an FTP or SFTP server, which always use forward slashes.</summary>
internal static class RemotePaths
{
    /// <summary>
    /// <paramref name="path"/> under the target's remote path; with no remote path it is relative
    /// to where the login lands.
    /// </summary>
    public static string Join(string root, string path)
    {
        var trimmedRoot = root.Replace('\\', '/').TrimEnd('/');
        var trimmedPath = path.Replace('\\', '/').Trim('/');

        var absolute = root.StartsWith('/') || root.StartsWith('\\');

        if (trimmedPath.Length == 0)
        {
            if (trimmedRoot.Length > 0)
                return trimmedRoot;

            return absolute ? "/" : ".";
        }

        return trimmedRoot.Length == 0 && !absolute ? trimmedPath : $"{trimmedRoot}/{trimmedPath}";
    }

    /// <summary>The folder and each above it, shortest first: <c>/a</c>, <c>/a/b</c>.</summary>
    public static IEnumerable<string> Ancestry(string fullPath)
    {
        var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = fullPath.StartsWith('/') ? "" : null;

        foreach (var part in parts)
        {
            current = current is null ? part : $"{current}/{part}";

            yield return current;
        }
    }
}
