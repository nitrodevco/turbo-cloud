using System.Globalization;
using System.Text.RegularExpressions;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Credit furni carry their value in the definition name, <c>CF_&lt;credits&gt;_&lt;name&gt;</c>
/// (the client reads the same number from the asset). Nothing else in the data says so.
/// </summary>
public static partial class CreditFurniValue
{
    [GeneratedRegex(@"^CF_(\d+)_", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool TryParse(string definitionName, out int credits)
    {
        credits = 0;

        var match = Pattern().Match(definitionName);

        return match.Success
            && int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out credits
            )
            && credits > 0;
    }
}
