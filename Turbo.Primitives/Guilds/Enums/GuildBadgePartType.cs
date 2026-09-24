namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Which of a badge's two part lists a row belongs to. The editor draws one base and up to four
/// symbols, and sends them to the server as one flat array; the type is what tells the base
/// apart from the symbols when the code is built.
/// </summary>
public enum GuildBadgePartType
{
    Base = 0,
    Symbol = 1,
}
