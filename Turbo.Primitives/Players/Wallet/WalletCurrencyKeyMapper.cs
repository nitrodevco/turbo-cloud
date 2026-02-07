using System;
using System.Collections.Generic;
using System.Globalization;

namespace Turbo.Primitives.Players.Wallet;

public static class WalletCurrencyKeyMapper
{
    public const string Credits = "credits";
    public const string Duckets = "duckets";

    private const string ActivityPointPrefix = "activitypoint_";

    public static bool IsCreditsKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return false;

        var normalized = Normalize(rawKey);
        return normalized is "credit" or Credits;
    }

    public static string ToCanonicalKey(
        string rawKey,
        WalletCurrencyKind kind,
        int activityPointType
    )
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new ArgumentException("Currency key is required.", nameof(rawKey));

        return kind switch
        {
            WalletCurrencyKind.Credits => Credits,
            WalletCurrencyKind.ActivityPoints => CanonicalActivityPointKey(activityPointType),
            _ => Normalize(rawKey),
        };
    }

    public static bool TryGetActivityPointType(string rawKey, out int activityPointType)
    {
        activityPointType = 0;

        if (string.IsNullOrWhiteSpace(rawKey))
            return false;

        var normalized = Normalize(rawKey);
        if (normalized is Duckets or "ducket")
            return true;

        if (
            normalized.StartsWith(ActivityPointPrefix, StringComparison.Ordinal)
            && int.TryParse(
                normalized[ActivityPointPrefix.Length..],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var prefixedType
            )
            && prefixedType >= 0
        )
        {
            activityPointType = prefixedType;
            return true;
        }

        if (
            int.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var numericType
            )
            && numericType >= 0
        )
        {
            activityPointType = numericType;
            return true;
        }

        return false;
    }

    public static IReadOnlySet<string> GetEquivalentKeys(
        string canonicalKey,
        WalletCurrencyKind kind,
        int activityPointType
    )
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Normalize(canonicalKey),
        };

        switch (kind)
        {
            case WalletCurrencyKind.Credits:
                keys.Add(Credits);
                keys.Add("credit");
                break;
            case WalletCurrencyKind.ActivityPoints:
            {
                var canonicalActivityKey = CanonicalActivityPointKey(activityPointType);
                keys.Add(canonicalActivityKey);
                keys.Add(activityPointType.ToString(CultureInfo.InvariantCulture));

                if (activityPointType == 0)
                {
                    keys.Add(Duckets);
                    keys.Add("ducket");
                    keys.Add($"{ActivityPointPrefix}0");
                }

                break;
            }
        }

        return keys;
    }

    private static string CanonicalActivityPointKey(int activityPointType)
    {
        if (activityPointType < 0)
            throw new ArgumentOutOfRangeException(
                nameof(activityPointType),
                "Activity point type must be non-negative."
            );

        if (activityPointType == 0)
            return Duckets;

        return $"{ActivityPointPrefix}{activityPointType}";
    }

    private static string Normalize(string rawKey) => rawKey.Trim().ToLowerInvariant();
}
