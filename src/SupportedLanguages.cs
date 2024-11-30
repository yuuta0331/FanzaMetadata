// ReSharper disable InconsistentNaming

using System.Collections.Generic;

namespace FanzaMetadata;

public enum SupportedLanguages
{
    ja_JP,
    en_US,
}

public static class TranslationDictionary
{
    public static readonly Dictionary<SupportedLanguages, string> ReleaseDate = new()
    {
        { SupportedLanguages.ja_JP, "配信開始日" },
        { SupportedLanguages.en_US, "Release date" },
    };

    public static readonly Dictionary<SupportedLanguages, string> UpdateDate = new()
    {
        { SupportedLanguages.ja_JP, "更新情報" },
        { SupportedLanguages.en_US, "Update information" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Series = new()
    {
        { SupportedLanguages.ja_JP, "シリーズ" },
        { SupportedLanguages.en_US, "Series" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Scenario = new()
    {
        { SupportedLanguages.ja_JP, "シナリオ" },
        { SupportedLanguages.en_US, "Scenario" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Illustration = new()
    {
        { SupportedLanguages.ja_JP, "原画" },
        { SupportedLanguages.en_US, "Illustration" },
    };

    public static readonly Dictionary<SupportedLanguages, string> VoiceActor = new()
    {
        { SupportedLanguages.ja_JP, "声優" },
        { SupportedLanguages.en_US, "Voice Actor" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Music = new()
    {
        { SupportedLanguages.ja_JP, "音楽" },
        { SupportedLanguages.en_US, "Music" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Author = new()
    {
        { SupportedLanguages.ja_JP, "作者" },
        { SupportedLanguages.en_US, "Author" },
    };

    public static readonly Dictionary<SupportedLanguages, string> Genre = new()
    {
        { SupportedLanguages.ja_JP, "ジャンル" },
        { SupportedLanguages.en_US, "Genre" },
    };
}