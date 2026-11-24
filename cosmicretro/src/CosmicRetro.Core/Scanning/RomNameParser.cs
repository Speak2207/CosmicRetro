using System.Text.RegularExpressions;

namespace CosmicRetro.Core.Scanning;

public sealed record ParsedRomName(string DisplayName, string Region, string Version);

public static partial class RomNameParser
{
    private static readonly IReadOnlyDictionary<string, string> RegionNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USA"] = "USA",
            ["U"] = "USA",
            ["Europe"] = "Europe",
            ["EuropeEn"] = "Europe",
            ["E"] = "Europe",
            ["Japan"] = "Japan",
            ["JapanEn"] = "Japan",
            ["J"] = "Japan",
            ["World"] = "World",
            ["W"] = "World",
            ["Korea"] = "Korea",
            ["K"] = "Korea",
            ["Brazil"] = "Brazil",
            ["B"] = "Brazil",
            ["France"] = "France",
            ["F"] = "France",
            ["Germany"] = "Germany",
            ["G"] = "Germany"
        };

    public static ParsedRomName Parse(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var regions = new List<string>();
        var versions = new List<string>();
        var displayName = QualifierPattern().Replace(stem, match =>
        {
            var value = match.Groups["value"].Value.Trim();
            if (RegionNames.TryGetValue(value, out var region))
            {
                regions.Add(region);
            }
            else if (VersionPattern().IsMatch(value))
            {
                versions.Add(value);
            }

            return " ";
        });

        displayName = WhitespacePattern().Replace(displayName, " ").Trim(' ', '-', '_', '.');
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = stem.Trim();
        }

        return new ParsedRomName(
            displayName,
            string.Join(", ", regions.Distinct(StringComparer.OrdinalIgnoreCase)),
            string.Join(", ", versions.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    [GeneratedRegex(@"[\[(]\s*(?<value>[^\])]+?)\s*[\])]", RegexOptions.CultureInvariant)]
    private static partial Regex QualifierPattern();

    [GeneratedRegex(@"(?i)(?:^|[\s._-])(?:rev(?:ision)?\.?\s*[a-z0-9.]+|v(?:ersion)?\.?\s*\d[\w.-]*|beta|prototype|proto|hack|translation|unreleased)(?:$|[\s._-])",
        RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
