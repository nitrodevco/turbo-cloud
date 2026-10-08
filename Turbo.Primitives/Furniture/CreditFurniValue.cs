using System.Globalization;
using System.Text.RegularExpressions;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Credit furni carry their value in the definition name (the client reads the same number from
/// the asset); nothing else in the data says so. Habbo names them three ways:
/// <c>CF_&lt;credits&gt;_&lt;name&gt;</c> (<c>CF_50_goldbar</c>), <c>CFC_&lt;credits&gt;_&lt;name&gt;</c>
/// (<c>CFC_500_goldbar</c>) and <c>CF_&lt;name&gt;_&lt;credits&gt;</c> (<c>CF_diamond_2500</c>):
/// the value is the first part of the name after its prefix that is a number.
/// </summary>
public static partial class CreditFurniValue
{
    [GeneratedRegex(@"^CFC?_(?:[^_]+_)*?(\d+)(?:_|$)", RegexOptions.CultureInvariant)]
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
