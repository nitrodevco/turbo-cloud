using System.Globalization;
using System.Text.RegularExpressions;

namespace Turbo.Primitives.Pets;

/// <summary>
/// Pet product codes as the catalog names them: <c>pet&lt;typeId&gt;</c>. The pet page asks for
/// sellable palettes by this code and a pet product's class name carries it.
/// </summary>
public static partial class PetProductCodes
{
    public const string PREFIX = "pet";

    [GeneratedRegex(@"^pet(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ProductCodeRegex();

    public static bool TryGetTypeId(string? productCode, out int typeId)
    {
        typeId = -1;

        if (string.IsNullOrWhiteSpace(productCode))
            return false;

        var match = ProductCodeRegex().Match(productCode.Trim());

        if (!match.Success)
            return false;

        typeId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

        return true;
    }

    public static string ForTypeId(int typeId) =>
        PREFIX + typeId.ToString(CultureInfo.InvariantCulture);
}
