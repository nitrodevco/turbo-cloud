using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Turbo.Primitives.Texts;

namespace Turbo.Tests.Support;

/// <summary>
/// Answers a faked <see cref="IHotelTextProvider"/> from a lookup, read on every call so a test
/// may change its texts after the fake is made. A key the lookup has no text for is missing.
/// </summary>
public static class HotelTextFakes
{
    public static void Use(Fakes fakes, Func<string, string?> lookup)
    {
        fakes.Handlers[nameof(IHotelTextProvider.GetTextAsync)] = call =>
            Task.FromResult(
                call.Args[0] is string key && lookup(key) is { Length: > 0 } text ? text : null
            );
        fakes.Handlers[nameof(IHotelTextProvider.GetTextsAsync)] = call =>
            Task.FromResult(Texts(((IEnumerable<string>)call.Args[0]!).Distinct(), lookup));
        // A family is only what the test asks of it: the lookup cannot list its keys.
        fakes.Handlers[nameof(IHotelTextProvider.GetTextsByPrefixAsync)] = _ =>
            Task.FromResult(HotelTexts.Empty);
    }

    public static void Use(Fakes fakes, IReadOnlyDictionary<string, string> texts) =>
        Use(fakes, key => texts.GetValueOrDefault(key));

    private static HotelTexts Texts(IEnumerable<string> keys, Func<string, string?> lookup)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in keys)
            if (lookup(key) is { } text)
                found[key] = text;

        return new HotelTexts(found);
    }
}
