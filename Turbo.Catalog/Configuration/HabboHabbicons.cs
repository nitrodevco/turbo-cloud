using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// Habbo's Habbicon collections as Habbo sent them to an account that owned none on 2026-10-17
/// (<c>HabbiconShopData</c>, hhus): duck, duck2 and frank, given and not sold, toast, sold
/// one by one for 5 credits and 5 diamonds or as a set for 50 and 50, and zombie, given by the
/// Habboween 2026 reward track. The rewards' names were not sent, so they are left empty.
/// </summary>
public static class HabboHabbicons
{
    private const string RESOURCE = "Turbo.Catalog.Resources.habbo-habbicons-2026-10-17.json";

    public static List<HabbiconCollectionDefinition> Collections()
    {
        using var stream =
            typeof(HabboHabbicons).Assembly.GetManifestResourceStream(RESOURCE)
            ?? throw new InvalidOperationException("Habbo's Habbicon collections are missing.");

        return JsonSerializer.Deserialize<List<HabbiconCollectionDefinition>>(stream)
            ?? throw new InvalidOperationException("Habbo's Habbicon collections are invalid.");
    }
}
