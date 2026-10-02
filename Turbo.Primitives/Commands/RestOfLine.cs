namespace Turbo.Primitives.Commands;

/// <summary>
/// An arguments-record parameter that takes everything left on the line as raw text, spaces
/// included. It must be the last parameter.
/// </summary>
public readonly record struct RestOfLine(string Text)
{
    public override string ToString() => Text;
}
