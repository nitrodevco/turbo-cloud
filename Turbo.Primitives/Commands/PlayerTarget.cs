namespace Turbo.Primitives.Commands;

/// <summary>
/// An operator command parameter that names a player, or a group of them with a selector
/// (<c>@room</c>, <c>@online</c>). Binding only keeps the text: the player may be offline, so the
/// name is looked up when the command runs, through <see cref="IOperatorCommandContext"/>.
/// </summary>
public readonly record struct PlayerTarget(string Text)
{
    public const char SELECTOR_PREFIX = '@';
    public const string ROOM = "@room";
    public const string ONLINE = "@online";

    public bool IsSelector => Text.Length > 0 && Text[0] == SELECTOR_PREFIX;

    public override string ToString() => Text;
}
