using System.Windows;

namespace GameTranslatorLens.Core;

public sealed record FrameSequenceMetadata(
    string CaseId,
    DateTime StartedAt,
    string AppVersion,
    CaptureRegion? CaptureRegion,
    int CaptureIntervalMs);

public sealed record FrameSequenceFrame(
    int FrameIndex,
    string CaseId,
    DateTime Timestamp,
    long ElapsedMs,
    string ImageFile,
    FrameSequenceRect CaptureRegion,
    IReadOnlyList<FrameSequenceOcrLine> RawOcrLines,
    IReadOnlyList<FrameSequenceOcrLine> ProcessedOcrLines,
    IReadOnlyList<FrameSequenceParsedLine> ParsedLines,
    IReadOnlyList<FrameSequenceParsedLine> CandidateLines,
    IReadOnlyList<FrameSequenceDecision> Decisions,
    IReadOnlyList<FrameSequenceParsedLine> NewLines,
    bool HasVisibleChat,
    bool ChatCycleJustReset);

public sealed record FrameSequenceOcrLine(string Text, FrameSequenceRect Bounds)
{
    public OcrTextLine ToLine() => new(Text, Bounds.ToRect());
}

public sealed record FrameSequenceParsedLine(
    string Speaker,
    string SourceText,
    FrameSequenceRect Bounds,
    IReadOnlyList<string> GlossaryHits);

public sealed record FrameSequenceDecision(
    string Speaker,
    string SourceText,
    FrameSequenceRect Bounds,
    bool Accepted,
    string Reason,
    string Key);

public sealed record FrameSequenceRect(double Left, double Top, double Width, double Height)
{
    public static FrameSequenceRect FromRect(Rect rect) =>
        new(rect.Left, rect.Top, rect.Width, rect.Height);

    public Rect ToRect() => new(Left, Top, Width, Height);
}
