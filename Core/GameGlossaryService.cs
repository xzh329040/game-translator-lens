using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameTranslatorLens.Core;

public sealed class GameGlossaryService
{
    private readonly List<GlossaryEntry> _entries = [];
    private readonly List<string> _ignorePhrases = [];

    public string Version { get; private set; } = "unknown";
    public int EntryCount => _entries.Count;


    /// <summary>
    /// Load glossary with common filters only (no game-specific filters).
    /// Used at startup before a game is selected.
    /// </summary>
    public static GameGlossaryService LoadDefault()
    {
        return LoadForGame(null);
    }

    /// <summary>
    /// Load glossary with common filters + the specified game's filter.
    /// Pass null or empty for common filters only.
    /// </summary>
    public static GameGlossaryService LoadForGame(string? gameId)
    {
        string baseDir = AppContext.BaseDirectory;
        string path = Path.Combine(baseDir, "Resources", "GameGlossary.zh-CN.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(Environment.CurrentDirectory, "Resources", "GameGlossary.zh-CN.json");
            baseDir = Environment.CurrentDirectory;
        }

        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        GlossaryFile file = JsonSerializer.Deserialize<GlossaryFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new GlossaryFile();

        GameGlossaryService service = new();
        service.Version = file.Version ?? "unknown";
        service._ignorePhrases.AddRange(file.IgnorePhrases ?? []);
        service._entries.AddRange(file.Entries ?? []);

        string filtersDir = Path.Combine(baseDir, "Resources", "Filters");
        LoadFilters(filtersDir, service._ignorePhrases, gameId);

        return service;
    }

    /// <summary>
    /// Reload filters for a different game at runtime. Clears old ignore phrases
    /// and re-loads common + game-specific filters. Glossary entries are preserved.
    /// </summary>
    public void ReloadFiltersForGame(string? gameId)
    {
        _ignorePhrases.Clear();

        string baseDir = AppContext.BaseDirectory;
        string path = Path.Combine(baseDir, "Resources", "GameGlossary.zh-CN.json");
        if (!File.Exists(path))
        {
            baseDir = Environment.CurrentDirectory;
        }

        // Reload ignore phrases from the main glossary file
        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        GlossaryFile? file = JsonSerializer.Deserialize<GlossaryFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (file?.IgnorePhrases is not null)
        {
            _ignorePhrases.AddRange(file.IgnorePhrases);
        }

        string filtersDir = Path.Combine(baseDir, "Resources", "Filters");
        LoadFilters(filtersDir, _ignorePhrases, gameId);
    }

    private static void LoadFilters(string filtersDir, List<string> ignorePhrases, string? gameId)
    {
        if (!Directory.Exists(filtersDir))
        {
            return;
        }

        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

        foreach (string filterPath in Directory.EnumerateFiles(filtersDir, "*.json"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filterPath);

            // Always load common.json; only load the selected game's filter
            bool isCommon = string.Equals(fileName, "common", StringComparison.OrdinalIgnoreCase);
            bool isSelectedGame = !string.IsNullOrEmpty(gameId)
                && string.Equals(fileName, gameId, StringComparison.OrdinalIgnoreCase);

            if (!isCommon && !isSelectedGame)
            {
                continue;
            }

            try
            {
                string content = File.ReadAllText(filterPath, System.Text.Encoding.UTF8);
                FilterFile? filter = JsonSerializer.Deserialize<FilterFile>(content, options);
                if (filter is { Enabled: true, IgnorePhrases: not null })
                {
                    ignorePhrases.AddRange(filter.IgnorePhrases);
                }
            }
            catch
            {
                // Skip malformed filter files silently — a bad filter shouldn't crash startup.
            }
        }
    }

    public string NormalizeOcrText(string text)
    {
        string result = text.Trim();
        result = Regex.Replace(result, @"\s+", " ");
        result = result.Replace("：", ":").Replace("﹕", ":").Replace("｜", "|");
        result = result.Replace("（", "(").Replace("）", ")");
        result = result.Replace("D Va", "D.Va", StringComparison.OrdinalIgnoreCase);
        result = Regex.Replace(result, @"\b([oO])\s*T\b", "OT");
        result = Regex.Replace(result, @"\bI\s+need\b", "I need", RegexOptions.IgnoreCase);
        return result;
    }

    public bool ShouldIgnoreLine(string text)
    {
        string normalized = NormalizeOcrText(text);
        if (normalized.Length < 2)
        {
            return true;
        }

        if (_ignorePhrases.Any(phrase => normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[\p{P}\p{S}\d\s]+$"))
        {
            return true;
        }

        return false;
    }

    public IReadOnlyList<GlossaryHit> FindHits(string text)
    {
        List<GlossaryHit> hits = [];
        string normalizedText = NormalizeKey(text);

        foreach (GlossaryEntry entry in _entries)
        {
            foreach (string term in entry.Terms ?? [])
            {
                string key = NormalizeKey(term);
                if (key.Length == 0)
                {
                    continue;
                }

                bool hit = key.Length <= 3
                    ? Regex.IsMatch(normalizedText, $@"(^| ){Regex.Escape(key)}( |$)")
                    : normalizedText.Contains(key, StringComparison.OrdinalIgnoreCase);

                if (hit)
                {
                    hits.Add(new GlossaryHit(term, entry.ZhCn ?? term, entry.Category ?? "term"));
                    break;
                }
            }
        }

        return hits
            .GroupBy(hit => hit.Target)
            .Select(group => group.First())
            .Take(12)
            .ToList();
    }

    public string ApplyTerms(string text)
    {
        string result = text;
        foreach (GlossaryHit hit in FindHits(text))
        {
            result = Regex.Replace(result, Regex.Escape(hit.Source), hit.Target, RegexOptions.IgnoreCase);
        }

        return result;
    }

    public string ApplyCustomPairs(string text, Dictionary<string, string> pairs)
    {
        if (pairs.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        string result = text;
        foreach ((string key, string value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // Case-insensitive whole-word replacement for short keys (<=3 chars),
            // substring replacement for longer keys (e.g. multi-word abbreviations).
            if (key.Length <= 3)
            {
                result = Regex.Replace(
                    result,
                    $@"\b{Regex.Escape(key)}\b",
                    value,
                    RegexOptions.IgnoreCase);
            }
            else
            {
                result = Regex.Replace(
                    result,
                    Regex.Escape(key),
                    value,
                    RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    public string BuildPromptContext(IReadOnlyList<GlossaryHit> hits)
    {
        if (hits.Count == 0)
        {
            return "无术语命中。";
        }

        return string.Join("; ", hits.Select(hit => $"{hit.Source}->{hit.Target}({hit.Category})"));
    }

    private static string NormalizeKey(string value)
    {
        string lower = value.ToLowerInvariant();
        lower = Regex.Replace(lower, @"[^\p{L}\p{N}]+", " ");
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }

    private sealed class GlossaryFile
    {
        public string? Version { get; set; }

        [JsonPropertyName("ignore_phrases")]
        public List<string>? IgnorePhrases { get; set; }

        public List<GlossaryEntry>? Entries { get; set; }

    }

    private sealed class FilterFile
    {
        public string? Game { get; set; }
        public string? Label { get; set; }
        public bool Enabled { get; set; }

        [JsonPropertyName("ignore_phrases")]
        public List<string>? IgnorePhrases { get; set; }
    }

    private sealed class GlossaryEntry
    {
        public string? Category { get; set; }

        [JsonPropertyName("zh_cn")]
        public string? ZhCn { get; set; }

        public List<string>? Terms { get; set; }
    }

}
