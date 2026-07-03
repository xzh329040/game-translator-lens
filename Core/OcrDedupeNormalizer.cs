using System.Text.RegularExpressions;

namespace GameTranslatorLens.Core;

public static class OcrDedupeNormalizer
{
    public static string NormalizeText(string value)
    {
        string normalized = value.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    public static string NormalizeSpeaker(string value)
    {
        string lower = value.ToLowerInvariant();
        return Regex.Replace(lower, @"[^\p{L}\p{N}]+", "");
    }

    public static bool IsSpeakerMatch(string left, string right)
    {
        if (left == right)
        {
            return true;
        }

        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        bool hasHangul = KoreanJamoNormalizer.ContainsHangul(compactLeft) ||
                         KoreanJamoNormalizer.ContainsHangul(compactRight);
        if (hasHangul)
        {
            string leftJamo = KoreanJamoNormalizer.NormalizeToJamo(compactLeft);
            string rightJamo = KoreanJamoNormalizer.NormalizeToJamo(compactRight);
            int jamoLimit = Math.Min(leftJamo.Length, rightJamo.Length);
            if (jamoLimit < 3 || Math.Abs(leftJamo.Length - rightJamo.Length) > 2)
            {
                return false;
            }

            return LevenshteinDistance(leftJamo, rightJamo) <= 1;
        }

        int limit = Math.Min(compactLeft.Length, compactRight.Length);
        if (limit < 5 || Math.Abs(compactLeft.Length - compactRight.Length) > 1)
        {
            return false;
        }

        return LevenshteinDistance(compactLeft, compactRight) <= 1;
    }

    public static bool IsSimilarText(string left, string right)
        => TextSimilarityScore(left, right) >= 0.76;

    public static double TextSimilarityScore(string left, string right)
    {
        if (left == right)
        {
            return 1;
        }

        bool hasHangul = KoreanJamoNormalizer.ContainsHangul(left) ||
                         KoreanJamoNormalizer.ContainsHangul(right);
        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        if (compactLeft == compactRight && compactLeft.Length > 0)
        {
            return 1;
        }

        if (hasHangul)
        {
            return HangulSimilarityScore(left, right, compactLeft, compactRight);
        }

        if (left.Length < 8 || right.Length < 8)
        {
            return CompactShortTextSimilarity(compactLeft, compactRight);
        }

        double best = 0;
        string shorter = left.Length <= right.Length ? left : right;
        string longer = left.Length <= right.Length ? right : left;
        if (longer.Contains(shorter, StringComparison.Ordinal) &&
            shorter.Length >= Math.Max(8, (int)(longer.Length * 0.65)))
        {
            best = Math.Max(best, (double)shorter.Length / longer.Length);
        }

        int commonPrefix = 0;
        int limit = Math.Min(left.Length, right.Length);
        while (commonPrefix < limit && left[commonPrefix] == right[commonPrefix])
        {
            commonPrefix++;
        }

        if (commonPrefix >= Math.Max(8, (int)(limit * 0.75)))
        {
            best = Math.Max(best, (double)commonPrefix / Math.Max(left.Length, right.Length));
        }

        best = Math.Max(best, TokenOverlapRatio(left, right));
        best = Math.Max(best, CharacterDiceRatio(left, right));
        best = Math.Max(best, CompactLevenshteinRatio(left, right));
        best = Math.Max(best, LongestCommonSubsequenceRatio(left, right));
        best = Math.Max(best, CompactTokenCoverageRatio(left, right));
        best = Math.Max(best, 1.0 - ((double)LevenshteinDistance(left, right) / Math.Max(left.Length, right.Length)));
        best = Math.Min(1, best + NumericAnchorBonus(left, right));
        return best;
    }

    private static double HangulSimilarityScore(string left, string right, string compactLeft, string compactRight)
    {
        double best = 0;
        if (compactLeft.Length > 0 && compactRight.Length > 0)
        {
            best = Math.Max(best, KoreanJamoNormalizer.JamoSimilarity(compactLeft, compactRight));
            best = Math.Max(best, CharacterDiceRatio(
                KoreanJamoNormalizer.NormalizeToJamo(compactLeft),
                KoreanJamoNormalizer.NormalizeToJamo(compactRight)));
            best = Math.Max(best, LongestCommonSubsequenceRatio(
                KoreanJamoNormalizer.NormalizeToJamo(compactLeft),
                KoreanJamoNormalizer.NormalizeToJamo(compactRight)));
        }

        if (Math.Min(compactLeft.Length, compactRight.Length) >= 2)
        {
            best = Math.Max(best, CompactLevenshteinRatio(left, right));
            best = Math.Max(best, CharacterDiceRatio(left, right));
            best = Math.Max(best, CompactTokenCoverageRatio(left, right) * 0.75);
        }

        return Math.Min(1, best + NumericAnchorBonus(left, right));
    }

    private static double CompactShortTextSimilarity(string compactLeft, string compactRight)
    {
        if (compactLeft.Length == 0 || compactRight.Length == 0)
        {
            return 0;
        }

        int maxLength = Math.Max(compactLeft.Length, compactRight.Length);
        if (maxLength < 3 || Math.Abs(compactLeft.Length - compactRight.Length) > 1)
        {
            return 0;
        }

        int distance = LevenshteinDistance(compactLeft, compactRight);
        return distance <= 1 ? 1.0 - ((double)distance / maxLength) : 0;
    }

    private static double TokenOverlapRatio(string left, string right)
    {
        string[] leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        int matched = 0;
        bool[] used = new bool[rightTokens.Length];
        foreach (string leftToken in leftTokens)
        {
            for (int i = 0; i < rightTokens.Length; i++)
            {
                if (used[i])
                {
                    continue;
                }

                if (leftToken == rightTokens[i] || AreTokensSimilar(leftToken, rightTokens[i]))
                {
                    used[i] = true;
                    matched++;
                    break;
                }
            }
        }

        return (double)matched / Math.Max(leftTokens.Length, rightTokens.Length);
    }

    private static bool AreTokensSimilar(string left, string right)
    {
        int minLength = Math.Min(left.Length, right.Length);
        if (minLength < 3 || Math.Abs(left.Length - right.Length) > 1)
        {
            return false;
        }

        return LevenshteinDistance(left, right) <= 1;
    }

    private static double CompactLevenshteinRatio(string left, string right)
    {
        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        if (compactLeft.Length < 6 || compactRight.Length < 6)
        {
            return 0;
        }

        int distance = LevenshteinDistance(compactLeft, compactRight);
        return 1.0 - ((double)distance / Math.Max(compactLeft.Length, compactRight.Length));
    }

    private static double LongestCommonSubsequenceRatio(string left, string right)
    {
        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        if (compactLeft.Length < 6 || compactRight.Length < 6)
        {
            return 0;
        }

        int lcs = LongestCommonSubsequenceLength(compactLeft, compactRight);
        return (double)lcs / Math.Max(compactLeft.Length, compactRight.Length);
    }

    private static double CompactTokenCoverageRatio(string left, string right)
    {
        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        string[] leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int useful = 0;
        int covered = 0;

        foreach (string token in leftTokens.Concat(rightTokens))
        {
            if (token.Length < 2 || Regex.IsMatch(token, @"^\d+$"))
            {
                continue;
            }

            useful++;
            string other = leftTokens.Contains(token, StringComparer.Ordinal) ? compactRight : compactLeft;
            if (other.Contains(token, StringComparison.Ordinal))
            {
                covered++;
            }
        }

        return useful == 0 ? 0 : (double)covered / useful;
    }

    private static double NumericAnchorBonus(string left, string right)
    {
        HashSet<string> leftNumbers = ExtractNumericAnchors(left);
        HashSet<string> rightNumbers = ExtractNumericAnchors(right);
        if (leftNumbers.Count == 0 || rightNumbers.Count == 0)
        {
            return 0;
        }

        int shared = leftNumbers.Count(value => rightNumbers.Contains(value));
        return shared == 0 ? 0 : Math.Min(0.12, shared * 0.06);
    }

    private static HashSet<string> ExtractNumericAnchors(string value)
    {
        HashSet<string> anchors = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(value, @"\d+\s*(?:초|s|sec|秒)?", RegexOptions.IgnoreCase))
        {
            string anchor = Regex.Replace(match.Value.ToLowerInvariant(), @"\s+", "");
            anchors.Add(anchor);
        }

        return anchors;
    }

    private static double CharacterDiceRatio(string left, string right)
    {
        string compactLeft = RemoveSpaces(left);
        string compactRight = RemoveSpaces(right);
        if (compactLeft.Length < 2 || compactRight.Length < 2)
        {
            return 0;
        }

        Dictionary<string, int> leftBigrams = CountBigrams(compactLeft);
        Dictionary<string, int> rightBigrams = CountBigrams(compactRight);
        int overlap = 0;
        foreach ((string key, int leftCount) in leftBigrams)
        {
            if (rightBigrams.TryGetValue(key, out int rightCount))
            {
                overlap += Math.Min(leftCount, rightCount);
            }
        }

        return (2.0 * overlap) / (compactLeft.Length - 1 + compactRight.Length - 1);
    }

    private static Dictionary<string, int> CountBigrams(string value)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        for (int i = 0; i < value.Length - 1; i++)
        {
            string bigram = value.Substring(i, 2);
            counts[bigram] = counts.TryGetValue(bigram, out int count) ? count + 1 : 1;
        }

        return counts;
    }

    private static int LongestCommonSubsequenceLength(string left, string right)
    {
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int i = 1; i <= left.Length; i++)
        {
            Array.Clear(current);
            for (int j = 1; j <= right.Length; j++)
            {
                current[j] = left[i - 1] == right[j - 1]
                    ? previous[j - 1] + 1
                    : Math.Max(previous[j], current[j - 1]);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string RemoveSpaces(string value) =>
        value.Replace(" ", "", StringComparison.Ordinal);

    private static int LevenshteinDistance(string left, string right)
    {
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
