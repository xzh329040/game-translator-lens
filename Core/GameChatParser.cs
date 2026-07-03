using System.Text.RegularExpressions;

namespace GameTranslatorLens.Core;

public sealed class GameChatParser
{
    private readonly GameGlossaryService _glossary;

    public GameChatParser(GameGlossaryService glossary)
    {
        _glossary = glossary;
    }

    public IReadOnlyList<ParsedChatLine> Parse(IReadOnlyList<OcrTextLine> lines)
    {
        return Parse(lines, universalMode: false, targetLanguage: "zh-CN", customPairs: null);
    }

    public IReadOnlyList<ParsedChatLine> Parse(IReadOnlyList<OcrTextLine> lines, bool universalMode, string targetLanguage, Dictionary<string, string>? customPairs = null)
    {
        List<ParsedChatLine> parsed = [];
        foreach (OcrTextLine line in lines.OrderBy(l => l.Bounds.Top))
        {
            string normalized = _glossary.NormalizeOcrText(line.Text);
            if (_glossary.ShouldIgnoreLine(normalized))
            {
                continue;
            }

            if (universalMode)
            {
                // Universal mode: translate every line, no chat format required
                if (normalized.Length < 2)
                {
                    continue;
                }

                string text = customPairs is { Count: > 0 }
                    ? _glossary.ApplyCustomPairs(normalized, customPairs)
                    : normalized;
                IReadOnlyList<GlossaryHit> hits = _glossary.FindHits(text);
                parsed.Add(new ParsedChatLine("", text, line.Bounds, hits));
            }
            else
            {
                // Chat mode: extract [PlayerName]: message format
                foreach ((string speaker, string message) in ExtractPlayerMessages(normalized))
                {
                    if (ShouldSkipForTarget(message, targetLanguage))
                    {
                        continue;
                    }

                    string text = customPairs is { Count: > 0 }
                        ? _glossary.ApplyCustomPairs(message, customPairs)
                        : message;
                    IReadOnlyList<GlossaryHit> hits = _glossary.FindHits(text);
                    parsed.Add(new ParsedChatLine(speaker, text, line.Bounds, hits));
                }
            }
        }

        return parsed;
    }

    private static IReadOnlyList<(string Speaker, string Message)> ExtractPlayerMessages(string text)
    {
        List<(string Speaker, string Message)> messages = [];

        foreach (Match match in Regex.Matches(
            text,
            @"(?:[\(（](?<group>[^\)）\r\n]{1,12})[\)）]\s*)?\[?(?<speaker>[^\s:：\[\]\(\)\r\n]{2,24})\]?\s*[:：]\s*(?<message>.*?)(?=\s*(?:[\(（][^\)）\r\n]{1,12}[\)）]\s*)?\[?[^\s:：\[\]\(\)\r\n]{2,24}\]?\s*[:：]|$)"))
        {
            string speaker = match.Groups["speaker"].Value.Trim();
            string message = match.Groups["message"].Value.Trim();
            if (speaker.Length == 0 || message.Length == 0)
            {
                continue;
            }

            string group = match.Groups["group"].Value.Trim();
            if (group.Length > 0)
            {
                speaker = $"[{group}] {speaker}";
            }

            messages.Add((speaker, message));
        }

        return messages;
    }

    private static bool ShouldSkipForTarget(string message, string targetLanguage)
    {
        if (message.Length < 2)
        {
            return true;
        }

        ScriptCounts scripts = CountScripts(message);

        return targetLanguage switch
        {
            "zh-CN" => IsDominantCjk(scripts),       // Skip Chinese if translating to Chinese
            "ja" => IsDominantKana(scripts),           // Skip Japanese if translating to Japanese
            "ko" => IsDominantHangul(scripts),         // Skip Korean if translating to Korean
            "en" => IsDominantLatin(scripts),           // Skip English if translating to English
            _ => IsDominantCjk(scripts)                 // Default: skip Chinese
        };
    }

    private static bool IsDominantCjk(ScriptCounts scripts)
    {
        return scripts.Cjk > 0 && scripts.Cjk >= Math.Max(2, scripts.TotalLetters / 2);
    }

    private static bool IsDominantKana(ScriptCounts scripts)
    {
        return scripts.Kana > 0 && scripts.Kana >= Math.Max(2, scripts.TotalLetters / 2);
    }

    private static bool IsDominantHangul(ScriptCounts scripts)
    {
        return scripts.Hangul > 0 && scripts.Hangul >= Math.Max(2, scripts.TotalLetters / 2);
    }

    private static bool IsDominantLatin(ScriptCounts scripts)
    {
        return scripts.Latin > 0 && scripts.Latin >= Math.Max(2, scripts.TotalLetters / 2);
    }

    private static ScriptCounts CountScripts(string message)
    {
        int hangul = 0;
        int kana = 0;
        int latin = 0;
        int cjk = 0;
        foreach (char ch in message)
        {
            if (ch is >= '가' and <= '힯' ||
                ch is >= 'ᄀ' and <= 'ᇿ' ||
                ch is >= '㄰' and <= '㆏')
            {
                hangul++;
            }
            else if (ch is >= '぀' and <= 'ヿ')
            {
                kana++;
            }
            else if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latin++;
            }
            else if (ch is >= '一' and <= '鿿')
            {
                cjk++;
            }
        }

        return new ScriptCounts(hangul, kana, latin, cjk);
    }

    private sealed record ScriptCounts(int Hangul, int Kana, int Latin, int Cjk)
    {
        public int TotalLetters => Hangul + Kana + Latin + Cjk;
    }
}
