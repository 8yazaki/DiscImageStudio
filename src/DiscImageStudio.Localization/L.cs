using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Localization;

/// <summary>A UI language the application ships a translation table for.</summary>
/// <param name="Code">Stable identifier stored in settings (BCP 47 style, e.g. "ja").</param>
/// <param name="NativeName">Name shown in the language picker, always in the language itself.</param>
public sealed record LanguageInfo(string Code, string NativeName);

/// <summary>
/// Runtime text localization. The source language (zh-CN) is the lookup key, so a missing
/// translation degrades to the original text instead of a blank or an identifier, and code
/// that has not been initialized behaves exactly as before localization existed.
/// <para>
/// <see cref="T"/> translates a fixed message; <see cref="F"/> translates a composite format
/// template ("已删除 {0} 个文件") and formats it, so translators may reorder placeholders.
/// <see cref="SourceOf"/> maps a translated message back to its source key for the few places
/// that branch on message identity.
/// </para>
/// The language is chosen once at startup (<see cref="Initialize"/>); it is intentionally not
/// switched while the application runs, and it never changes <see cref="CultureInfo.CurrentCulture"/>,
/// so number parsing and formatting keep following the user's regional settings.
/// </summary>
public static class L
{
    public const string SourceLanguage = "zh-CN";

    /// <summary>Setting value meaning "follow the operating system UI language".</summary>
    public const string AutoSetting = "auto";

    /// <summary>Environment variable that selects the language for command-line runs.</summary>
    public const string EnvironmentVariable = "DISC_IMAGE_STUDIO_LANG";

    public static IReadOnlyList<LanguageInfo> SupportedLanguages { get; } =
    [
        new(SourceLanguage, "简体中文"),
        new("en", "English"),
        new("ja", "日本語"),
    ];

    private sealed record State(
        string Language,
        FrozenDictionary<string, string> Table,
        FrozenDictionary<string, string> Reverse);

    private static readonly FrozenDictionary<string, string> Empty =
        new Dictionary<string, string>().ToFrozenDictionary();

    private static State _state = new(SourceLanguage, Empty, Empty);

    /// <summary>The language code currently in effect.</summary>
    public static string Language => _state.Language;

    /// <summary>Selects the UI language. Accepts <see cref="AutoSetting"/>, null/empty (both
    /// follow the OS UI language) or a language code; unknown values fall back to automatic
    /// detection. Returns the language that is now in effect.</summary>
    public static string Initialize(string? setting)
    {
        string language = Resolve(setting);
        FrozenDictionary<string, string> table = language == SourceLanguage ? Empty : Load(language);
        Dictionary<string, string> reverse = new(StringComparer.Ordinal);
        foreach ((string source, string translated) in table)
        {
            // First writer wins; keys that are compared by identity are checked for
            // uniqueness by the localization tests.
            reverse.TryAdd(translated, source);
        }

        _state = new State(language, table, reverse.ToFrozenDictionary(StringComparer.Ordinal));
        return language;
    }

    /// <summary>Normalizes a setting or OS culture to one of <see cref="SupportedLanguages"/>.</summary>
    public static string Resolve(string? setting)
    {
        string? code = Normalize(setting);
        if (code is not null)
        {
            return code;
        }

        return Normalize(CultureInfo.CurrentUICulture.Name) ?? "en";
    }

    /// <summary>Maps a code such as "ja-JP", "zh_Hans" or "EN" to a supported code, or null
    /// when it is empty, automatic or not supported.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value.Trim().Replace('_', '-');
        if (text.Equals(AutoSetting, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string primary = text.Split('-')[0].ToLowerInvariant();
        return primary switch
        {
            "zh" => SourceLanguage,
            "ja" => "ja",
            "en" => "en",
            _ => null,
        };
    }

    /// <summary>Translates a fixed message; unknown text is returned unchanged.</summary>
    public static string T(string source)
        => _state.Table.TryGetValue(source, out string? translated) ? translated : source;

    /// <summary>Translates a composite format template and formats it with the current
    /// culture. A translation with a broken placeholder falls back to the source template
    /// rather than throwing at the user.</summary>
    public static string F(string source, params object?[] arguments)
        => F(CultureInfo.CurrentCulture, source, arguments);

    /// <summary>Same as <see cref="F(string, object?[])"/> with an explicit format provider,
    /// for call sites that deliberately format numbers invariantly.</summary>
    public static string F(IFormatProvider provider, string source, params object?[] arguments)
    {
        string template = T(source);
        try
        {
            return string.Format(provider, template, arguments);
        }
        catch (FormatException)
        {
            return string.Format(provider, source, arguments);
        }
    }

    /// <summary>Returns the source-language text a translated message came from, or the
    /// message itself when it is not a known translation. This lets code keep matching on the
    /// original wording whatever language is active.</summary>
    public static string SourceOf(string message)
        => _state.Reverse.TryGetValue(message, out string? source) ? source : message;

    private static FrozenDictionary<string, string> Load(string language)
    {
        string resource = $"DiscImageStudio.Locales.{language}.json";
        try
        {
            using Stream? stream = typeof(L).Assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                return Empty;
            }

            Dictionary<string, string>? table = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (table is null)
            {
                return Empty;
            }

            // Blank values are placeholders for "not translated yet".
            return table.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToFrozenDictionary(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or IOException or NotSupportedException)
        {
            return Empty;
        }
    }

    /// <summary>Reads a translation table directly from the embedded resources. Used by tests
    /// and tooling; the running application goes through <see cref="Initialize"/>.</summary>
    public static IReadOnlyDictionary<string, string> LoadTable(string language) => Load(language);
}
