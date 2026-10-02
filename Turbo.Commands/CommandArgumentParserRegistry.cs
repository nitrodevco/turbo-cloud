using System;
using System.Collections.Generic;
using System.Threading;
using Turbo.Primitives.Commands;

namespace Turbo.Commands;

public sealed class CommandArgumentParserRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, ICommandArgumentParser> _parsers = [];

    public ICommandArgumentParser? Find(Type type)
    {
        lock (_gate)
            return _parsers.GetValueOrDefault(type);
    }

    public IDisposable Register(IReadOnlyList<ICommandArgumentParser> parsers)
    {
        lock (_gate)
        {
            var seen = new HashSet<Type>();
            foreach (var parser in parsers)
                if (_parsers.ContainsKey(parser.ValueType) || !seen.Add(parser.ValueType))
                    throw new InvalidOperationException(
                        $"An argument parser already owns {parser.ValueType}."
                    );
            foreach (var parser in parsers)
                _parsers.Add(parser.ValueType, parser);
        }
        return new Registration(this, parsers);
    }

    private sealed class Registration(
        CommandArgumentParserRegistry owner,
        IReadOnlyList<ICommandArgumentParser> parsers
    ) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
                foreach (var parser in parsers)
                    if (ReferenceEquals(owner._parsers.GetValueOrDefault(parser.ValueType), parser))
                        owner._parsers.Remove(parser.ValueType);
        }
    }
}
