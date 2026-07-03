using System.Windows;

namespace GameTranslatorLens.Core;

public sealed class ChatTimeline
{
    private readonly List<ChatMessage> _messages = [];
    private readonly int _capacity;
    private long _nextSeq = 1;

    public ChatTimeline(int capacity = 100)
    {
        _capacity = Math.Max(1, capacity);
    }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public ChatMessage AddDetected(ParsedChatLine line, long frameId, DateTime? timestamp = null)
    {
        DateTime now = timestamp ?? DateTime.Now;
        ChatMessage message = new(
            _nextSeq++,
            line.Speaker,
            line.SourceText,
            line.Bounds,
            line.GlossaryHits,
            now,
            frameId);
        _messages.Add(message);
        TrimToCapacity();
        return message;
    }

    public void Observe(ChatMessage message, ParsedChatLine line, long frameId, DateTime? timestamp = null)
    {
        message.Speaker = line.Speaker;
        message.Bounds = Rect.Union(message.Bounds, line.Bounds);
        message.GlossaryHits = line.GlossaryHits;
        message.LastSeenAt = timestamp ?? DateTime.Now;
        message.LastSeenFrameId = frameId;
        message.SeenCount++;
        message.AddObservation(line.SourceText);
    }

    public IReadOnlyList<ChatMessage> TailWindow(int maxCount)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<ChatMessage>();
        }

        return _messages.Count <= maxCount
            ? _messages.ToArray()
            : _messages.TakeLast(maxCount).ToArray();
    }

    public void Clear()
    {
        _messages.Clear();
        _nextSeq = 1;
    }

    public void MarkQueued(ChatMessage message)
    {
        message.State = ChatMessageState.Queued;
    }

    public void MarkTranslating(ChatMessage message)
    {
        message.State = ChatMessageState.Translating;
    }

    public void MarkTranslated(ChatMessage message, string translation)
    {
        message.Translation = translation;
        message.State = ChatMessageState.Translated;
    }

    public void MarkFailed(ChatMessage message)
    {
        message.RetryCount++;
        message.State = ChatMessageState.Failed;
    }

    private void TrimToCapacity()
    {
        if (_messages.Count <= _capacity)
        {
            return;
        }

        _messages.RemoveRange(0, _messages.Count - _capacity);
    }
}

public sealed class ChatMessage
{
    private readonly List<string> _variants;
    private readonly List<string> _observations;

    public ChatMessage(
        long seq,
        string speaker,
        string sourceText,
        Rect bounds,
        IReadOnlyList<GlossaryHit> glossaryHits,
        DateTime firstSeenAt,
        long firstSeenFrameId)
    {
        Seq = seq;
        Speaker = speaker;
        ConsensusText = sourceText;
        Bounds = bounds;
        GlossaryHits = glossaryHits;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = firstSeenAt;
        LastSeenFrameId = firstSeenFrameId;
        _variants = [sourceText];
        _observations = [sourceText];
        LastObservedText = sourceText;
    }

    public long Seq { get; }
    public string Speaker { get; set; }
    public string ConsensusText { get; private set; }
    public string LastObservedText { get; private set; }
    public string? PreviousObservedText { get; private set; }
    public Rect Bounds { get; set; }
    public IReadOnlyList<GlossaryHit> GlossaryHits { get; set; }
    public IReadOnlyList<string> Variants => _variants;
    public IReadOnlyList<string> Observations => _observations;
    public int SeenCount { get; set; } = 1;
    public long LastSeenFrameId { get; set; }
    public ChatMessageState State { get; set; } = ChatMessageState.Detected;
    public string? Translation { get; set; }
    public int RetryCount { get; set; }
    public DateTime FirstSeenAt { get; }
    public DateTime LastSeenAt { get; set; }

    public bool IsReadyForTranslation(int maxConfirmationObservations = 2)
    {
        if (SeenCount < 2)
        {
            return false;
        }

        if (AreConsecutiveObservationsEquivalent(PreviousObservedText, LastObservedText))
        {
            return true;
        }

        return ConfirmationObservationCount >= maxConfirmationObservations;
    }

    public int ConfirmationObservationCount => Math.Max(0, SeenCount - 1);

    public void AddObservation(string text)
    {
        PreviousObservedText = LastObservedText;
        LastObservedText = text;
        _observations.Add(text);
        if (!_variants.Contains(text, StringComparer.Ordinal))
        {
            _variants.Add(text);
        }

        ConsensusText = ChooseConsensusText(_observations);
    }

    private static string ChooseConsensusText(IReadOnlyList<string> variants) =>
        variants
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Key.Length)
            .First()
            .Key;

    private static bool AreConsecutiveObservationsEquivalent(string? previous, string current)
    {
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        string normalizedPrevious = OcrDedupeNormalizer.NormalizeText(previous);
        string normalizedCurrent = OcrDedupeNormalizer.NormalizeText(current);
        if (string.Equals(normalizedPrevious, normalizedCurrent, StringComparison.Ordinal))
        {
            return true;
        }

        bool hasHangul = KoreanJamoNormalizer.ContainsHangul(normalizedPrevious) ||
                         KoreanJamoNormalizer.ContainsHangul(normalizedCurrent);
        return hasHangul && KoreanJamoNormalizer.JamoEditDistance(normalizedPrevious, normalizedCurrent) <= 1;
    }
}

public enum ChatMessageState
{
    Detected,
    Confirming,
    Queued,
    Translating,
    Translated,
    Failed
}
