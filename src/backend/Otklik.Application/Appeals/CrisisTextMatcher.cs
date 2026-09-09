using System.Text.RegularExpressions;

namespace Otklik.Application.Appeals;

public sealed record CrisisMarkerDefinition(string Pattern, string RiskType);

public sealed record CrisisDetectionResult(bool IsMatch, string? RiskType)
{
    public static readonly CrisisDetectionResult None = new(false, null);
}

public static partial class CrisisTextMatcher
{
    public static CrisisDetectionResult Detect(
        IEnumerable<string?> textParts,
        IEnumerable<CrisisMarkerDefinition> markers)
    {
        var textTokens = Tokenize(string.Join(' ', textParts.Where(part => !string.IsNullOrWhiteSpace(part))));
        if (textTokens.Length == 0) return CrisisDetectionResult.None;

        foreach (var marker in markers)
        {
            var patternTokens = Tokenize(marker.Pattern);
            if (patternTokens.Length == 0 || patternTokens.Length > textTokens.Length) continue;

            for (var start = 0; start < textTokens.Length; start++)
            {
                if (!TokenMatches(patternTokens[0], textTokens[start])) continue;
                var candidateIndex = start;
                var allMatch = true;
                for (var patternIndex = 1; patternIndex < patternTokens.Length; patternIndex++)
                {
                    var foundAt = -1;
                    var lastCandidate = Math.Min(textTokens.Length - 1, candidateIndex + 3);
                    for (var next = candidateIndex + 1; next <= lastCandidate; next++)
                    {
                        if (!TokenMatches(patternTokens[patternIndex], textTokens[next])) continue;
                        foundAt = next;
                        break;
                    }
                    if (foundAt < 0) { allMatch = false; break; }
                    candidateIndex = foundAt;
                }

                if (allMatch) return new CrisisDetectionResult(true, marker.RiskType);
            }
        }

        return CrisisDetectionResult.None;
    }

    private static string[] Tokenize(string value) => TokenRegex()
        .Matches(value.ToLowerInvariant().Replace('ё', 'е'))
        .Select(match => match.Value)
        .ToArray();

    private static bool TokenMatches(string pattern, string candidate)
    {
        if (pattern.EndsWith('*'))
        {
            var stem = pattern[..^1];
            if (candidate.StartsWith(stem, StringComparison.Ordinal)) return true;
            return stem.Length >= 4
                && candidate.Length >= stem.Length
                && EditDistanceAtMostOne(stem, candidate[..stem.Length]);
        }

        if (string.Equals(pattern, candidate, StringComparison.Ordinal)) return true;
        return pattern.Length >= 4
            && candidate.Length >= 4
            && EditDistanceAtMostOne(pattern, candidate);
    }

    private static bool EditDistanceAtMostOne(string left, string right)
    {
        if (Math.Abs(left.Length - right.Length) > 1) return false;
        if (left == right) return true;

        var leftIndex = 0;
        var rightIndex = 0;
        var edits = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (left[leftIndex] == right[rightIndex])
            {
                leftIndex++;
                rightIndex++;
                continue;
            }

            if (++edits > 1) return false;
            if (left.Length > right.Length) leftIndex++;
            else if (right.Length > left.Length) rightIndex++;
            else
            {
                leftIndex++;
                rightIndex++;
            }
        }

        return edits + (leftIndex < left.Length || rightIndex < right.Length ? 1 : 0) <= 1;
    }

    [GeneratedRegex("[\\p{L}\\p{Nd}*]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
