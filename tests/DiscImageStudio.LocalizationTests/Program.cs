using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiscImageStudio.Localization;

// Console test runner (same style as DiscImageStudio.ArchitectureTests): exits non-zero on failure.
int failures = 0;

void Check(bool condition, string name)
{
    if (!condition)
    {
        failures++;
        Console.Error.WriteLine("FAIL: " + name);
    }
}

Dictionary<string, string> LoadTable(string code)
{
    using Stream stream = typeof(L).Assembly.GetManifestResourceStream($"DiscImageStudio.Locales.{code}.json")
        ?? throw new InvalidOperationException("Missing locale resource " + code);
    return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
}

static string[] Placeholders(string text)
    => Regex.Matches(text, @"\{\d+(?::[^}]*)?\}").Select(match => match.Value).Order().ToArray();

Dictionary<string, string> en = LoadTable("en");
Dictionary<string, string> ja = LoadTable("ja");

// --- tables -----------------------------------------------------------------------------
Check(en.Count > 0 && en.Count == ja.Count, "en/ja contain the same number of entries");
Check(en.Keys.ToHashSet().SetEquals(ja.Keys), "en/ja contain the same keys");
foreach ((string language, Dictionary<string, string> table) in new[] { ("en", en), ("ja", ja) })
{
    foreach ((string key, string value) in table)
    {
        Check(!string.IsNullOrWhiteSpace(value), $"{language}: blank translation for '{key}'");
        Check(
            Placeholders(key).SequenceEqual(Placeholders(value)),
            $"{language}: placeholders differ for '{key}'");
        Check(
            key.EndsWith('\n') == value.EndsWith('\n') && key.StartsWith('\n') == value.StartsWith('\n'),
            $"{language}: leading/trailing newline differs for '{key}'");
    }
}

Check(!en.Values.Any(value => Regex.IsMatch(value, @"[぀-ヿ一-鿿]")),
    "en translations contain no CJK characters");

// --- behaviour --------------------------------------------------------------------------
Check(L.Initialize("zh-CN") == "zh-CN", "zh-CN resolves");
Check(L.T("取消") == "取消", "source language returns the key unchanged");
Check(L.T("没有这句话") == "没有这句话", "unknown text falls back to the key");

Check(L.Initialize("en") == "en", "en resolves");
Check(L.T("取消") == "Cancel", "en translates a fixed message");
Check(L.T("没有这句话") == "没有这句话", "en: unknown text falls back to the key");
Check(L.SourceOf("Cancel") == "取消", "SourceOf maps a translation back to its key");
Check(L.SourceOf("取消") == "取消", "SourceOf leaves source text alone");
Check(L.SourceOf("not a message") == "not a message", "SourceOf leaves unknown text alone");

Check(L.Initialize("ja") == "ja", "ja resolves");
Check(L.T("取消") == "キャンセル", "ja translates a fixed message");
Check(L.F("点 {0}", 3) == "点 3", "ja formats a template");

string pitch = L.F(CultureInfo.InvariantCulture, "完整数据区半径（创建图案时）：{0}–{1} mm", 24.5, 58);
Check(pitch.Contains("24.5") && pitch.Contains("58"), "provider overload formats with the given culture");

// A translation with a broken placeholder must not throw at the user.
Check(L.F("取消 {0}", "x") == "取消 x", "template without translation formats the key");

foreach ((string input, string expected) in new[]
{
    ("zh", "zh-CN"), ("zh-Hans", "zh-CN"), ("zh_CN", "zh-CN"), ("ja-JP", "ja"), ("JA", "ja"), ("en-US", "en"),
})
{
    Check(L.Normalize(input) == expected, $"Normalize({input})");
}

Check(L.Normalize("auto") is null && L.Normalize("") is null && L.Normalize(null) is null && L.Normalize("fr") is null,
    "Normalize rejects auto/empty/unsupported");
Check(L.Resolve("fr") is "zh-CN" or "en" or "ja", "Resolve always yields a supported language");
Check(L.SupportedLanguages.Select(language => language.Code).SequenceEqual(["zh-CN", "en", "ja"]),
    "supported languages");

// Every translated message round-trips through SourceOf, apart from keys that share a translation.
foreach (string code in new[] { "en", "ja" })
{
    L.Initialize(code);
    Dictionary<string, string> table = code == "en" ? en : ja;
    var shared = table.GroupBy(pair => pair.Value).Where(group => group.Count() > 1).SelectMany(group => group.Select(pair => pair.Key)).ToHashSet();
    foreach (string key in table.Keys.Where(key => !shared.Contains(key)))
    {
        Check(L.SourceOf(L.T(key)) == key, $"{code}: SourceOf(T('{key}')) round-trips");
    }
}

L.Initialize("zh-CN");

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} localization check(s) failed.");
    return 1;
}

Console.WriteLine($"Localization tests passed ({en.Count} entries x 2 languages).");
return 0;
