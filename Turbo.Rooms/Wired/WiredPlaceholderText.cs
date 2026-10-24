using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>Parsing of the "name" or "name\tdelimiter" string param of the placeholder addons.</summary>
public static class WiredPlaceholderText
{
    /// <summary>
    /// How a player writes a placeholder in a wired text: its sigil and its name in brackets,
    /// "$(name)" for the text output add-ons and "#(name)" for the capturer
    /// (<c>PlaceholderNameSection</c>: "Use this by typing $(name) in Wired texts").
    /// </summary>
    public static string Token(char sigil, string name) => $"{sigil}({name})";

    /// <summary>The name inside a token: <c>name</c> for <c>$(name)</c>; empty for an empty token.</summary>
    public static string NameOf(string token) => token.Length > 3 ? token[2..^1] : string.Empty;

    /// <summary>
    /// The placeholder "WIRED Trigger: User Says Keyword" creates for its stack, filled with what
    /// was said (Wired Faculty announcement, 12/10/2026: "Placeholder $(trigger_text)
    /// automatically created for WIRED Trigger: User Says Keyword").
    /// </summary>
    public const string TRIGGER_TEXT_NAME = "trigger_text";

    /// <summary><c>$(trigger_text)</c>, as a text writes it.</summary>
    public static readonly string TriggerTextToken = Token('$', TRIGGER_TEXT_NAME);

    private const string DEFAULT_DELIMITER = ", ";

    /// <summary>
    /// Whether another placeholder of <paramref name="box"/>'s kind on its stack has the name
    /// <paramref name="stringParam"/> gives: Habbo refuses that save with
    /// <c>wiredfurni.error.link.destination_conflict</c> (official client capture 2026-10-24, W3 lab
    /// addon.14.R2 / addon.15.R2 / addon.19.R2, 3/3). Placeholders of two kinds were not tried.
    /// </summary>
    public static bool IsNameTakenOnStack(
        IWiredTextPlaceholder box,
        IEnumerable<IRoomFloorItem> stack,
        string? stringParam
    )
    {
        var (name, _) = SplitNameAndDelimiter(stringParam);

        if (name.Length == 0)
            return false;

        var token = Token('$', name);

        return stack.Any(item =>
            item.Logic is IWiredTextPlaceholder other
            && !ReferenceEquals(other, box)
            && other.GetType() == box.GetType()
            && other.Token == token
        );
    }

    /// <summary>
    /// The placeholders that fill a text: of two with one name, which can share a stack when moved
    /// there by hand, the one with the higher item id, whatever their order on the stack or which
    /// was saved last (official client capture 2026-10-24, W3 lab addon.14/15/19.X5, 4/4).
    /// </summary>
    public static IEnumerable<IWiredTextPlaceholder> HighestIdPerName(
        IReadOnlyList<IWiredTextPlaceholder> placeholders
    ) =>
        placeholders.Where(placeholder =>
            !placeholders.Any(other =>
                !ReferenceEquals(other, placeholder)
                && other.Token.Length > 0
                && other.Token == placeholder.Token
                && ItemId(other) > ItemId(placeholder)
            )
        );

    private static int ItemId(IWiredTextPlaceholder placeholder) =>
        placeholder is FurnitureWiredLogic box ? box.ObjectId.Value : 0;

    public static (string name, string delimiter) SplitNameAndDelimiter(string? stringParam)
    {
        if (string.IsNullOrWhiteSpace(stringParam))
            return (string.Empty, DEFAULT_DELIMITER);

        var index = stringParam.IndexOf('\t');

        if (index < 0)
            return (stringParam.Trim(), DEFAULT_DELIMITER);

        var delimiter = stringParam[(index + 1)..];

        return (stringParam[..index].Trim(), delimiter.Length == 0 ? DEFAULT_DELIMITER : delimiter);
    }
}
