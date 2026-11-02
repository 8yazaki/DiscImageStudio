# Localization

Disc Image Studio's UI is available in **Simplified Chinese (zh-CN, the source language), English (en) and Japanese (ja)**.

## How it works

- Every user-visible string in the code is written in Chinese and wrapped in a lookup call. The Chinese text **is the key** (gettext style):
  - `L.T("取消")` — fixed message.
  - `L.F("已删除 {0} 个文件", count)` — composite format template; translators may reorder `{0}`, `{1}`… and keep format specifiers such as `{0:N0}`.
  - XAML: `Text="{loc:T '取消'}"` (single-quoted; escape `\'`, `\{`, `\}` and `\\` inside it).
  - `L.SourceOf(message)` maps a translated message back to its Chinese key. Use it only where code has to branch on a message's identity (a few calibration warnings and IMAPI operation names), never to parse user-facing text otherwise.
- Translation tables are `src/DiscImageStudio.Localization/Locales/<code>.json` (flat `{ "中文原文": "translation" }`), embedded into the dependency-free `DiscImageStudio.Localization` assembly. Missing or blank entries fall back to the Chinese text, so an incomplete table never shows an empty label.
- The language is chosen once at startup and applied after a restart: GUI = `settings.json` → `language` (`auto`, `zh-CN`, `en`, `ja`; `auto` follows the OS UI language, anything unsupported becomes English), command line = `--lang <code>` or the `DISC_IMAGE_STUDIO_LANG` environment variable, defaulting to English regardless of the OS or GUI language so logs look the same everywhere. Output that was Chinese before this change (and may be parsed by old scripts) is now English; pass `--lang zh-CN` to get the original text.
- `CultureInfo.CurrentCulture` is never changed: number and date formatting still follow the user's regional settings. The UI language only selects text.
- Japanese switches the UI font to the system Yu Gothic UI / Meiryo UI (the bundled HarmonyOS Sans SC has Simplified-Chinese glyph shapes and no kana).
- Built-in disc preset names and descriptions (`built-in-disc-presets.json`) are translated when the built-in document is loaded. User presets and project files are never translated or rewritten.
- What is **not** translated: the technical documents under `docs/`, the license texts, exception messages from the operating system, and screenshots in the READMEs.

## Fixing a translation

1. Edit the value in `Locales/en.json` or `Locales/ja.json`. Keep placeholders (`{0}`, `{1:0.###}`…) and leading/trailing spaces or newlines exactly as in the key.
2. Run `python tools/check_locales.py` (every source string present, no orphans, placeholders match) and `dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release`.

## Adding or changing source strings

1. Write the Chinese text inside `L.T` / `L.F` / `{loc:T '…'}`. Do not build sentences by concatenating translated pieces unless the pieces are meant to be read in order; prefer one `L.F` template.
2. Do not use `L.T` in a `const` or in a static initializer that can run before `L.Initialize` (use a property: `static string X => L.T("…")`).
3. Run `python tools/check_locales.py --list` to see the new keys, then add them to `en.json` and `ja.json`.

## Adding a language

1. Add `Locales/<code>.json` (it is embedded automatically by the `Locales\*.json` glob).
2. Add the language to `L.SupportedLanguages` and to `L.Normalize`, and add its native name to the picker (it is generated from `SupportedLanguages`).
3. If its script needs a different font, extend `AppHost.ApplyLanguageFonts`.
4. Extend `tools/check_locales.py` / the localization tests if needed, and add a `README.<code>.md` (the default `README.md` is English; `README.zh-CN.md` is the Chinese original).
