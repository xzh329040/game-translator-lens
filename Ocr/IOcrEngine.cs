using System.Drawing;
using GameTranslatorLens.Core;

namespace GameTranslatorLens.Ocr;

public interface IOcrEngine
{
    string Name { get; }
    bool IsReady { get; }
    string? InitError { get; }
    Task<IReadOnlyList<OcrTextLine>> RecognizeAsync(Bitmap bitmap, string languageCode, CancellationToken cancellationToken);
}
