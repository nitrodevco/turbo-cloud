using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Turbo.Plugins;

/// <summary>
/// A formula of an <c>arrange</c> op (nitro-next <c>templatePatchArrange.ts</c>; <c>docs/template-patches.md</c>,
/// "An arrangement as data"): numbers, <c>'text'</c>, <c>true</c> / <c>false</c> / <c>null</c>,
/// <c>${&lt;element&gt;.&lt;measure&gt;}</c>, <c>${window.&lt;measure&gt;}</c>, <c>${ctx.&lt;key&gt;}</c>
/// and <c>${style:&lt;name&gt;}</c>; <c>+ - * / %</c>, comparisons, <c>&amp;&amp; || !</c>, <c>a ? b : c</c>,
/// brackets and the client's functions. Only read here, never worked out: what is wrong with one is
/// said as the client says it, so the panel refuses what no client could run before it is saved.
/// </summary>
internal static partial class TemplateArrangeFormula
{
    /// <summary>What a formula may read of an element.</summary>
    public static readonly string[] MEASURES =
    [
        "x",
        "y",
        "width",
        "height",
        "textWidth",
        "textHeight",
    ];

    /// <summary>The functions a formula may call, and how many values each takes (at least, at most).</summary>
    private static readonly Dictionary<string, (int Min, int Max)> FUNCTIONS = new()
    {
        ["min"] = (1, int.MaxValue),
        ["max"] = (1, int.MaxValue),
        ["round"] = (1, 1),
        ["floor"] = (1, 1),
        ["ceil"] = (1, 1),
        ["abs"] = (1, 1),
        ["oneOf"] = (2, int.MaxValue),
    };

    private static readonly string[] OPERATORS =
    [
        "||",
        "&&",
        "==",
        "!=",
        "<=",
        ">=",
        "<",
        ">",
        "+",
        "-",
        "*",
        "/",
        "%",
        "!",
        "?",
        ":",
        "(",
        ")",
        ",",
    ];

    private enum Kind
    {
        Value,
        Ref,
        Name,
        Operator,
    }

    private sealed record Token(Kind Kind, string Text);

    /// <summary>What is wrong with <paramref name="formula"/>, as one sentence; null for one a client runs.</summary>
    public static string? Problem(string formula)
    {
        try
        {
            new Parser(Tokenize(formula)).Whole();

            return null;
        }
        catch (FormatException e)
        {
            return e.Message;
        }
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var at = 0;

        while (at < text.Length)
        {
            var rest = text[at..];

            if (char.IsWhiteSpace(rest[0]))
            {
                at++;

                continue;
            }

            var number = NumberPattern().Match(rest);

            if (number.Success)
            {
                tokens.Add(new Token(Kind.Value, number.Value));
                at += number.Length;

                continue;
            }

            if (rest.StartsWith("${", StringComparison.Ordinal))
            {
                var end = rest.IndexOf('}');

                if (end < 0)
                    throw new FormatException($"a ${{ has no closing }} ({rest})");

                var name = rest[2..end];

                if (name.Contains("${", StringComparison.Ordinal))
                    throw new FormatException(
                        $"a placeholder is inside another ({rest[..(end + 1)]}): they do not nest"
                    );

                tokens.Add(new Token(Kind.Ref, name));
                at += end + 1;

                continue;
            }

            if (rest[0] == '\'')
            {
                var quoted = StringPattern().Match(rest);

                if (!quoted.Success)
                    throw new FormatException($"a text in quotes has no closing quote ({rest})");

                tokens.Add(new Token(Kind.Value, quoted.Value));
                at += quoted.Length;

                continue;
            }

            var word = NamePattern().Match(rest);

            if (word.Success)
            {
                tokens.Add(new Token(Kind.Name, word.Value));
                at += word.Length;

                continue;
            }

            var op =
                OPERATORS.FirstOrDefault(x => rest.StartsWith(x, StringComparison.Ordinal))
                ?? throw new FormatException($"'{rest[0]}' is no part of a formula");

            tokens.Add(new Token(Kind.Operator, op));
            at += op.Length;
        }

        return tokens;
    }

    private static void Reference(string name)
    {
        if (name.StartsWith("ctx.", StringComparison.Ordinal) && name.Length > 4)
            return;

        if (StylePattern().IsMatch(name))
            return;

        if (
            name.StartsWith("item.", StringComparison.Ordinal)
            || name.StartsWith("text.", StringComparison.Ordinal)
        )
            throw new FormatException(
                $"${{{name}}}: an arrange reads the window, its elements and ${{ctx.<key>}}, not a row's or a text's value"
            );

        var dot = name.LastIndexOf('.');

        if (dot <= 0 || !MEASURES.Contains(name[(dot + 1)..]))
            throw new FormatException(
                $"${{{name}}} reads nothing: write ${{<element>.<measure>}} ({string.Join(", ", MEASURES)}), ${{window.<measure>}} or ${{ctx.<key>}}"
            );
    }

    /// <summary>The client's recursive descent, loosest first: <c>?:</c>, <c>||</c>, <c>&amp;&amp;</c>, comparison, sums, products, unary.</summary>
    private sealed class Parser(List<Token> tokens)
    {
        private int _at;

        public void Whole()
        {
            Expression();

            if (_at < tokens.Count)
                throw new FormatException($"'{tokens[_at].Text}' follows a whole formula");
        }

        private bool Peek(string op) =>
            _at < tokens.Count && tokens[_at].Kind == Kind.Operator && tokens[_at].Text == op;

        private void Expect(string op)
        {
            if (!Peek(op))
                throw new FormatException(
                    $"'{op}' was expected {(_at < tokens.Count ? $"before '{tokens[_at].Text}'" : "at the end")}"
                );

            _at++;
        }

        private void Expression()
        {
            Binary(4);

            if (!Peek("?"))
                return;

            _at++;
            Expression();
            Expect(":");
            Expression();
        }

        private static readonly string[][] LEVELS =
        [
            ["*", "/", "%"],
            ["+", "-"],
            ["==", "!=", "<=", ">=", "<", ">"],
            ["&&"],
            ["||"],
        ];

        private void Binary(int level)
        {
            if (level < 0)
            {
                Unary();

                return;
            }

            Binary(level - 1);

            while (LEVELS[level].Any(Peek))
            {
                _at++;
                Binary(level - 1);
            }
        }

        private void Unary()
        {
            if (Peek("-") || Peek("!"))
            {
                _at++;
                Unary();

                return;
            }

            Primary();
        }

        private void Primary()
        {
            if (_at >= tokens.Count)
                throw new FormatException("the formula ends where a value was expected");

            var token = tokens[_at++];

            switch (token.Kind)
            {
                case Kind.Value:
                    return;
                case Kind.Ref:
                    Reference(token.Text);

                    return;
                case Kind.Name when token.Text is "true" or "false" or "null":
                    return;
                case Kind.Name:
                {
                    if (!FUNCTIONS.TryGetValue(token.Text, out var arity))
                        throw new FormatException(
                            $"'{token.Text}' is no function ({string.Join(", ", FUNCTIONS.Keys)}); an element is read as ${{<element>.<measure>}}"
                        );

                    Expect("(");

                    var count = 0;

                    if (!Peek(")"))
                    {
                        do
                        {
                            Expression();
                            count++;
                        } while (Peek(",") && ++_at > 0);
                    }

                    Expect(")");

                    if (count < arity.Min || count > arity.Max)
                        throw new FormatException(
                            $"{token.Text} takes {(arity.Min == arity.Max ? $"{arity.Min}" : $"at least {arity.Min}")} value(s), not {count}"
                        );

                    return;
                }
                case Kind.Operator when token.Text == "(":
                    Expression();
                    Expect(")");

                    return;
                default:
                    throw new FormatException($"'{token.Text}' is where a value was expected");
            }
        }
    }

    [GeneratedRegex(@"^(?:0x[0-9a-fA-F]+|\d+(?:\.\d+)?|\.\d+)")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^'(?:[^'\\]|\\.)*'")]
    private static partial Regex StringPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^style:[A-Za-z0-9._:-]+$")]
    private static partial Regex StylePattern();
}
