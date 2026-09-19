namespace Turbo.Primitives.Pets;

/// <summary>
/// The extra parameter of a pet purchase: <c>name\npaletteId\ncolor</c> as the client's pet
/// page composes it, with the colour as hex digits without <c>#</c>.
/// </summary>
public sealed record PetPurchaseData
{
    public const char SEPARATOR = '\n';
    private const int FIELD_COUNT = 3;

    public required string Name { get; init; }
    public required int PaletteId { get; init; }
    public required string Color { get; init; }

    public static bool TryParse(string? extraParam, out PetPurchaseData data)
    {
        data = null!;

        if (string.IsNullOrWhiteSpace(extraParam))
            return false;

        var fields = extraParam.Split(SEPARATOR);

        if (fields.Length != FIELD_COUNT || !int.TryParse(fields[1], out var paletteId))
            return false;

        if (!PetFigure.IsValidColor(fields[2]))
            return false;

        data = new PetPurchaseData
        {
            Name = fields[0].Trim(),
            PaletteId = paletteId,
            Color = fields[2].Trim().ToUpperInvariant(),
        };

        return true;
    }
}
