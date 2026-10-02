namespace Turbo.Primitives.Commands;

public readonly record struct CommandToken(string Text, int Start, int End, bool Valid);
