namespace GameTranslatorLens.Core;

/// <summary>
/// Per-game profile holding game-specific custom translation pairs.
/// Each game gets its own set so switching games swaps both filter rules and custom translations.
/// </summary>
public sealed class GameProfile
{
    public Dictionary<string, string> CustomTranslationPairs { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}