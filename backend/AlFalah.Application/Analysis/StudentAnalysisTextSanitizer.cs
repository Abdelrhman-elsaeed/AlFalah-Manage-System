using System.Text.RegularExpressions;

namespace AlFalah.Application.Analysis;

/// <summary>Removes provider chat-template leakage from the end of generated reports.</summary>
public static partial class StudentAnalysisTextSanitizer
{
    public static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var marker = GeneratedRoleMarker().Match(text);
        if (!marker.Success) return text.Trim();

        var lines = text[..marker.Index].Split(["\r\n", "\n", "\r"], StringSplitOptions.None).ToList();
        while (lines.Count > 0)
        {
            var tail = lines[^1].Trim();
            if (tail.Length == 0 || ArtifactOnlyLine().IsMatch(tail)) lines.RemoveAt(lines.Count - 1);
            else break;
        }

        var clean = string.Join('\n', lines).TrimEnd();
        if (clean.Count(character => character == '`') % 2 != 0)
            clean = clean[..clean.LastIndexOf('`')].TrimEnd();
        return EmptyEmphasisAtEnd().Replace(clean, string.Empty).Trim();
    }

    [GeneratedRegex(@"point\s*:\s*(?:user|assistant)\s*\|", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedRoleMarker();

    [GeneratedRegex("""^(?:[*_`"'#|:.\-–—]+|آ)$""", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactOnlyLine();

    [GeneratedRegex(@"\s*\*+\s+\*+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyEmphasisAtEnd();
}
