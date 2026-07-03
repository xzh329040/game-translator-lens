using GameTranslatorLens.Core;

namespace GameTranslatorLens.Translation;

public static class TranslationProviderFactory
{
    public static ITranslationProvider Create(AppSettings settings, GameGlossaryService glossary)
    {
        return settings.TranslationProvider switch
        {
            "DeepSeek" => new OpenAICompatibleTranslationProvider(settings, glossary),
            "OpenAI Compatible" => new OpenAICompatibleTranslationProvider(settings, glossary),
            _ => new OpenAICompatibleTranslationProvider(settings, glossary)
        };
    }
}
