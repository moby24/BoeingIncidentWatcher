using System.Text.RegularExpressions;
using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Filtering;

internal sealed class IncidentFilter : IIncidentFilter
{
    private static readonly Regex FlightNumberRegex = new(
        @"\b(?:flight|flt)\s*(?:[A-Z]{2,3}\s*)?\d{2,4}[A-Z]?\b|\b(?:AA|UA|DL|AS|WN|BA|AF|KL|LH|EK|QR|AI|IX|UPS|FDX|ET|JT|MU|CA|CZ|QF|NZ)\s?\d{2,4}[A-Z]?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegistrationRegex = new(
        @"\bN[1-9][0-9]{0,4}[A-Z]{0,2}\b|\b(?:VT|G|D|F|C|B|JA|HL|HS|PK|9V|9M|RP|VH|ZK|TC|A6|A7|HZ|LN|OY|SE|EI|PH|EC|CS|XA|OB|CC|LV|PR|PT|ZS|ET|SU|UR|RA|UP|UK|4X|OE)-[A-Z0-9]{3,5}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AircraftTypeRegex = new(
        @"\b(?:B7[0-9]{2}|B7[0-9]{2}[A-Z]|B73[3-9]|B74[1-8]|B75[2-7]|B76[2-7]|B77[2-9]|B78[7-9]|B38M|B39M|B3XM|B738|B739|A2(?:20|21)|A3(?:18|19|20|21|30|32|33|39|40|50|59|80|88|89)|A220|A320|A321|A330|A350|A380|E1(?:70|75|90|95)|E2(?:90|95)|CRJ[1279]|AT(?:43|45|72|75)|DH8[A-D]|DHC8|MD8[0-9]|MD11|DC10)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] IncidentKeywords =
    {
        "accident",
        "crash",
        "crashed",
        "incident",
        "emergency",
        "collision",
        "collided",
        "fire",
        "smoke",
        "engine failure",
        "runway",
        "landing gear",
        "door plug",
        "fuel switch",
        "fuel-switch",
        "evacuation",
        "fatal",
        "killed",
        "injured",
        "grounded after"
    };

    private readonly bool _requireIncidentIdentifier;

    public IncidentFilter(bool requireIncidentIdentifier)
    {
        _requireIncidentIdentifier = requireIncidentIdentifier;
    }

    public NewsItem? Apply(NewsItem item)
    {
        if (!_requireIncidentIdentifier)
        {
            return item;
        }

        var haystack = BuildFilterText(item);
        if (!ContainsIncidentKeyword(haystack))
        {
            return null;
        }

        var flightMatch = FlightNumberRegex.Match(haystack);
        if (flightMatch.Success)
        {
            item.Identifier = NormalizeIdentifier(flightMatch.Value);
            item.MatchReason = "flight number";
            return item;
        }

        var registrationMatch = RegistrationRegex.Match(haystack);
        if (registrationMatch.Success)
        {
            item.Identifier = NormalizeIdentifier(registrationMatch.Value);
            item.MatchReason = "aircraft registration";
            return item;
        }

        var aircraftTypeMatch = AircraftTypeRegex.Match(haystack);
        if (aircraftTypeMatch.Success)
        {
            item.Identifier = NormalizeIdentifier(aircraftTypeMatch.Value);
            item.MatchReason = "aircraft type";
            return item;
        }

        return null;
    }

    private static bool ContainsIncidentKeyword(string value)
    {
        return IncidentKeywords.Any(keyword => value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string BuildFilterText(NewsItem item)
    {
        var title = StripSourceSuffix(item.Title);
        var description = StripSourceSuffix(item.Description);
        return (title + " " + description).Trim();
    }

    private static string StripSourceSuffix(string value)
    {
        var text = (value ?? string.Empty).Replace('\u00a0', ' ');
        var sourceSeparator = text.LastIndexOf(" - ", StringComparison.Ordinal);
        if (sourceSeparator > 0)
        {
            return text[..sourceSeparator];
        }

        sourceSeparator = text.LastIndexOf("  ", StringComparison.Ordinal);
        if (sourceSeparator > 0)
        {
            return text[..sourceSeparator];
        }

        return text;
    }

    private static string NormalizeIdentifier(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
    }
}
