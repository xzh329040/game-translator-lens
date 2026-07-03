using System.Diagnostics;
using System.Drawing;

namespace OcrPreprocessLab;

internal sealed class EdgeProjectionGate
{
    private const int SampleStep = 2;
    private const int MinimumScore = 46;

    public EdgeProjectionGateResult Observe(Bitmap bitmap)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        SampledFrame frame = Sample(bitmap);
        bool[] mask = BuildTextLikeMask(frame);
        ProjectionSummary projection = AnalyzeProjection(mask, frame.Width, frame.Height);
        int maskPixels = mask.Count(static value => value);
        double density = maskPixels / (double)Math.Max(1, mask.Length);
        double score = Math.Min(
            100,
            projection.StrongLineCount * 34 +
            projection.WeakLineCount * 13 +
            Math.Min(20, projection.WidestOriginalSpan / 8.0) +
            Math.Min(12, projection.MaxSegmentsInLine * 2.0) +
            Math.Min(14, density * 900));

        bool triggered = projection.StrongLineCount >= 1 && score >= MinimumScore;
        string reason = triggered
            ? "edge-projection-line"
            : projection.WeakLineCount > 0
                ? $"weak-edge-lines-{projection.WeakLineCount}-span-{projection.WidestOriginalSpan}"
                : "no-edge-lines";

        stopwatch.Stop();
        return new EdgeProjectionGateResult(
            triggered,
            score,
            projection.StrongLineCount,
            projection.WeakLineCount,
            projection.WidestOriginalSpan,
            projection.MaxSegmentsInLine,
            density,
            reason,
            stopwatch.Elapsed);
    }

    private static SampledFrame Sample(Bitmap bitmap)
    {
        int width = Math.Max(1, (bitmap.Width + SampleStep - 1) / SampleStep);
        int height = Math.Max(1, (bitmap.Height + SampleStep - 1) / SampleStep);
        int[] luminance = new int[width * height];
        int[] saturation = new int[width * height];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Math.Min(bitmap.Height - 1, y * SampleStep);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Math.Min(bitmap.Width - 1, x * SampleStep);
                Color color = bitmap.GetPixel(sourceX, sourceY);
                int index = y * width + x;
                luminance[index] = ToLuminance(color);
                saturation[index] = Math.Max(color.R, Math.Max(color.G, color.B)) -
                                    Math.Min(color.R, Math.Min(color.G, color.B));
            }
        }

        return new SampledFrame(width, height, luminance, saturation);
    }

    private static bool[] BuildTextLikeMask(SampledFrame frame)
    {
        bool[] mask = new bool[frame.Width * frame.Height];
        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                int index = y * frame.Width + x;
                int luminance = frame.Luminance[index];
                int saturation = frame.Saturation[index];
                int contrast = GetLocalContrast(frame, x, y, luminance);

                bool paleGlyphEdge = luminance >= 150 && saturation <= 135 && contrast >= 18;
                bool coloredGlyphEdge = luminance >= 102 && saturation >= 42 && contrast >= 16;
                bool outlinedGlyphEdge = luminance >= 95 && contrast >= 42;
                if (paleGlyphEdge || coloredGlyphEdge || outlinedGlyphEdge)
                {
                    mask[index] = true;
                }
            }
        }

        return mask;
    }

    private static ProjectionSummary AnalyzeProjection(bool[] mask, int width, int height)
    {
        List<RowSummary> candidateRows = [];
        for (int y = 0; y < height; y++)
        {
            RowSummary row = AnalyzeRow(mask, width, y);
            if (row.PixelCount >= 3 && row.SegmentCount >= 1)
            {
                candidateRows.Add(row);
            }
        }

        int strongLineCount = 0;
        int weakLineCount = 0;
        int widestSpan = 0;
        int maxSegments = 0;
        List<RowSummary> band = [];
        foreach (RowSummary row in candidateRows)
        {
            if (band.Count == 0 || row.Y - band[^1].Y <= 2)
            {
                band.Add(row);
                continue;
            }

            FlushBand(band);
            band.Clear();
            band.Add(row);
        }

        FlushBand(band);
        return new ProjectionSummary(strongLineCount, weakLineCount, widestSpan, maxSegments);

        void FlushBand(List<RowSummary> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            int span = (rows.Max(static row => row.MaxX) - rows.Min(static row => row.MinX) + 1) * SampleStep;
            int maxPixelCount = rows.Max(static row => row.PixelCount);
            int segmentCount = rows.Max(static row => row.SegmentCount);
            widestSpan = Math.Max(widestSpan, span);
            maxSegments = Math.Max(maxSegments, segmentCount);

            bool strong = rows.Count >= 2 &&
                          span >= 28 &&
                          (segmentCount >= 2 || maxPixelCount >= 9);
            bool weak = span >= 16 &&
                        (segmentCount >= 1 || maxPixelCount >= 5);
            if (strong)
            {
                strongLineCount++;
            }
            else if (weak)
            {
                weakLineCount++;
            }
        }
    }

    private static RowSummary AnalyzeRow(bool[] mask, int width, int y)
    {
        int rowOffset = y * width;
        int pixels = 0;
        int segments = 0;
        int minX = width;
        int maxX = -1;
        bool inSegment = false;
        for (int x = 0; x < width; x++)
        {
            if (!mask[rowOffset + x])
            {
                inSegment = false;
                continue;
            }

            pixels++;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            if (!inSegment)
            {
                segments++;
                inSegment = true;
            }
        }

        return new RowSummary(y, pixels, segments, minX, maxX);
    }

    private static int GetLocalContrast(SampledFrame frame, int x, int y, int currentLuminance)
    {
        int maxDelta = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            int sampleY = Math.Clamp(y + dy, 0, frame.Height - 1);
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                int sampleX = Math.Clamp(x + dx, 0, frame.Width - 1);
                maxDelta = Math.Max(maxDelta, Math.Abs(currentLuminance - frame.Luminance[sampleY * frame.Width + sampleX]));
            }
        }

        return maxDelta;
    }

    private static int ToLuminance(Color color) =>
        (color.R * 299 + color.G * 587 + color.B * 114) / 1000;

    private sealed record SampledFrame(int Width, int Height, int[] Luminance, int[] Saturation);

    private sealed record RowSummary(int Y, int PixelCount, int SegmentCount, int MinX, int MaxX);

    private sealed record ProjectionSummary(
        int StrongLineCount,
        int WeakLineCount,
        int WidestOriginalSpan,
        int MaxSegmentsInLine);
}

internal sealed record EdgeProjectionGateResult(
    bool HasLikelyText,
    double Score,
    int StrongLineCount,
    int WeakLineCount,
    int WidestOriginalSpan,
    int MaxSegmentsInLine,
    double Density,
    string Reason,
    TimeSpan Elapsed);
