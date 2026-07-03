using System.Text.RegularExpressions;
using System.Windows;

namespace GameTranslatorLens.Core;

public static partial class OcrTextPostProcessor
{
    public static IReadOnlyList<OcrTextLine> Process(IReadOnlyList<OcrTextLine> lines)
    {
        List<OcrTextLine> result = [];
        foreach (OcrTextLine line in lines.OrderBy(static line => line.Bounds.Top))
        {
            string text = RepairPlayerBoundary(line.Text.Trim());
            if (text.Length == 0)
            {
                continue;
            }

            if (result.Count > 0 &&
                IsContinuationCandidate(text) &&
                LooksLikePlayerMessage(result[^1].Text) &&
                IsLikelyWrappedLine(result[^1].Bounds, line.Bounds))
            {
                OcrTextLine previous = result[^1];
                result[^1] = new OcrTextLine(
                    previous.Text + " " + text,
                    Rect.Union(previous.Bounds, line.Bounds));
                continue;
            }

            result.Add(new OcrTextLine(text, line.Bounds));
        }

        // Phase 2: Merge player-name header lines with the following message line.
        // In some games the player name badge renders as a separate UI element,
        // so OCR outputs "hanxu :" and "Smoking second floor" as two lines.
        List<OcrTextLine> merged = [];
        for (int i = 0; i < result.Count; i++)
        {
            if (i + 1 < result.Count &&
                IsPlayerNameHeader(result[i].Text) &&
                IsMessageBodyLine(result[i + 1].Text))
            {
                string combined = result[i].Text.TrimEnd() + " " + result[i + 1].Text.TrimStart();
                Rect combinedBounds = Rect.Union(result[i].Bounds, result[i + 1].Bounds);
                merged.Add(new OcrTextLine(combined, combinedBounds));
                i++;
            }
            else
            {
                merged.Add(result[i]);
            }
        }

        return merged;
    }

    private static bool IsPlayerNameHeader(string text)
    {
        if (text.Length is < 3 or > 22)
        {
            return false;
        }

        // Must end with colon (half or full-width)
        if (!text.EndsWith(':') && !text.EndsWith('：'))
        {
            return false;
        }

        // Should not contain spaces (player names are single tokens)
        if (text.Contains(' '))
        {
            return false;
        }

        // Must contain at least some letters
        return HasChatScript(text);
    }

    private static bool IsMessageBodyLine(string text)
    {
        if (text.Length < 2)
        {
            return false;
        }

        // A message body line must NOT itself be a player name header
        if (IsPlayerNameHeader(text))
        {
            return false;
        }

        // Must contain chat-relevant script
        return HasChatScript(text);
    }

    private static string RepairPlayerBoundary(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        Match missingRightBracket = MissingRightBracketRegex().Match(text);
        if (missingRightBracket.Success)
        {
            return $"[{missingRightBracket.Groups["speaker"].Value.Trim()}]: {missingRightBracket.Groups["message"].Value.Trim()}";
        }

        Match slashAsBracket = SlashAsBracketRegex().Match(text);
        if (slashAsBracket.Success)
        {
            return $"[{slashAsBracket.Groups["speaker"].Value.Trim()}]: {slashAsBracket.Groups["message"].Value.Trim()}";
        }

        Match missingColon = MissingColonRegex().Match(text);
        if (missingColon.Success && HasChatScript(missingColon.Groups["message"].Value))
        {
            return $"[{missingColon.Groups["speaker"].Value.Trim()}]: {missingColon.Groups["message"].Value.Trim()}";
        }

        return text;
    }

    private static bool LooksLikePlayerMessage(string text) =>
        PlayerMessageRegex().IsMatch(text);

    private static bool IsContinuationCandidate(string text)
    {
        ScriptCounts scripts = CountScripts(text);
        return !LooksLikePlayerMessage(text) &&
               !text.Contains('[') &&
               !text.Contains(']') &&
               !text.Contains('(') &&
               !text.Contains(')') &&
               !IsCjkDominantWithoutHangul(scripts) &&
               text.Length <= 48 &&
               scripts.HasChatScript;
    }

    private static bool IsLikelyWrappedLine(Rect previous, Rect current)
    {
        if (current.Top < previous.Top)
        {
            return false;
        }

        double verticalGap = current.Top - previous.Bottom;
        double maxGap = Math.Max(10, previous.Height * 1.6);
        if (verticalGap > maxGap)
        {
            return false;
        }

        return current.Left >= previous.Left - 12 &&
               current.Left <= previous.Right + 24;
    }

    private static bool HasChatScript(string text) =>
        CountScripts(text).HasChatScript;

    private static bool IsCjkDominantWithoutHangul(ScriptCounts scripts) =>
        scripts.Hangul == 0 &&
        scripts.Cjk > 0 &&
        scripts.Cjk >= Math.Max(2, scripts.TotalLetters / 2);

    private static ScriptCounts CountScripts(string text)
    {
        int hangul = 0;
        int kana = 0;
        int latin = 0;
        int cjk = 0;
        foreach (char ch in text)
        {
            if (ch is >= '\uAC00' and <= '\uD7AF' ||
                ch is >= '\u1100' and <= '\u11FF' ||
                ch is >= '\u3130' and <= '\u318F')
            {
                hangul++;
            }
            else if (ch is >= '\u3040' and <= '\u30FF')
            {
                kana++;
            }
            else if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latin++;
            }
            else if (ch is >= '\u4E00' and <= '\u9FFF')
            {
                cjk++;
            }
        }

        return new ScriptCounts(hangul, kana, latin, cjk);
    }

    private sealed record ScriptCounts(int Hangul, int Kana, int Latin, int Cjk)
    {
        public int TotalLetters => Hangul + Kana + Latin + Cjk;
        public bool HasChatScript => Hangul > 0 || Kana > 0 || Latin > 0;
    }

    [GeneratedRegex(@"^\[(?<speaker>[^\]\[:：/\\]{2,24})\s*[:：]\s*(?<message>.+)$")]
    private static partial Regex MissingRightBracketRegex();

    [GeneratedRegex(@"^\[(?<speaker>[^\]\[:：/\\]{2,24})\s*[/\\]\s*[:：]?\s*(?<message>.+)$")]
    private static partial Regex SlashAsBracketRegex();

    [GeneratedRegex(@"^\[(?<speaker>[^\]\r\n]{2,24})\]\s+(?<message>.+)$")]
    private static partial Regex MissingColonRegex();

    [GeneratedRegex(@"(?:^\[[^\]\r\n]{2,24}\]|^[^\s:：\[\]\r\n]{2,24})\s*[:：]")]
    private static partial Regex PlayerMessageRegex();
}
