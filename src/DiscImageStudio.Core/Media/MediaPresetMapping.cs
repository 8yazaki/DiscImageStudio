using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscImageStudio.Core.Media;

/// <summary>A built-in hint: discs whose identity text contains <see cref="IdContains"/> are
/// the model the listed measured presets were calibrated on.</summary>
public sealed record MediaPresetRule(
    MediaFamily Family,
    string IdContains,
    IReadOnlyList<string> PresetIds,
    string Name);

public static class BuiltInMediaRules
{
    /// <summary>Only well-known media codes of discs that have measured presets. A model that is
    /// not listed is not "unsupported": the user can store the preset they calibrated for it.</summary>
    public static IReadOnlyList<MediaPresetRule> All { get; } = Array.AsReadOnly(
    [
        new MediaPresetRule(MediaFamily.Dvd, "MCC 03RG",
            ["verbatim-dvd-r-azo-43533", "verbatim-dvd-r-azo-43523"], "Verbatim DVD-R AZO"),
        new MediaPresetRule(MediaFamily.Dvd, "MCC 004",
            ["verbatim-dvd-plus-r-azo-43498"], "Verbatim DVD+R AZO"),
    ]);
}

/// <summary>A preset the user tied to one disc model (and, informationally, the drive).</summary>
public sealed record UserMediaMapping(
    string MediaId,
    MediaFamily Family,
    string PresetId,
    string? DriveName,
    DateTimeOffset SavedAt);

/// <summary>
/// The remembered "this disc model ↔ this calibrated preset" pairs, in
/// <c>%LOCALAPPDATA%\DiscImageStudio\media-presets.json</c>. A missing or damaged file reads as
/// empty and is never an error: it only costs the user one re-selection.
/// </summary>
public sealed class UserMediaPresetStore
{
    public const string FileName = "media-presets.json";
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;

    public UserMediaPresetStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public static string GetDefaultPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscImageStudio",
            FileName);

    public IReadOnlyList<UserMediaMapping> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            using FileStream stream = File.OpenRead(_path);
            StoreDocument? document = JsonSerializer.Deserialize<StoreDocument>(stream, JsonOptions);
            if (document is null || document.SchemaVersion != SchemaVersion || document.Mappings is null)
            {
                return [];
            }

            return document.Mappings
                .Where(item => item is not null
                    && !string.IsNullOrWhiteSpace(item.MediaId)
                    && !string.IsNullOrWhiteSpace(item.PresetId)
                    && Enum.IsDefined(item.Family)
                    && item.Family != MediaFamily.Unknown)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or NotSupportedException)
        {
            return [];
        }
    }

    public UserMediaMapping? Find(MediaFamily family, string? mediaId)
    {
        string key = MediaIdentity.Normalize(mediaId);
        if (key.Length == 0)
        {
            return null;
        }

        return Load().FirstOrDefault(item => item.Family == family
            && MediaIdentity.Normalize(item.MediaId) == key);
    }

    /// <summary>Stores the pairing, replacing an earlier one for the same disc model.</summary>
    public void Remember(UserMediaMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        string key = MediaIdentity.Normalize(mapping.MediaId);
        if (key.Length == 0 || mapping.Family == MediaFamily.Unknown || string.IsNullOrWhiteSpace(mapping.PresetId))
        {
            throw new ArgumentException("A remembered pairing needs a disc identity, family and preset.", nameof(mapping));
        }

        List<UserMediaMapping> items = Load()
            .Where(item => !(item.Family == mapping.Family && MediaIdentity.Normalize(item.MediaId) == key))
            .ToList();
        items.Add(mapping with { MediaId = key });

        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new StoreDocument(SchemaVersion, items), JsonOptions);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal sealed record StoreDocument(int SchemaVersion, List<UserMediaMapping>? Mappings);
}

public enum MediaMatchKind
{
    None,
    UserSaved,
    BuiltIn,
}

public sealed record MediaPresetMatch(
    MediaMatchKind Kind,
    string? PresetId,
    IReadOnlyList<string> AlternativePresetIds,
    string? Label)
{
    public static MediaPresetMatch NoMatch { get; } = new(MediaMatchKind.None, null, [], null);
}

public static class MediaPresetResolver
{
    /// <summary>The user's own pairing wins over a built-in hint. Preset ids that no longer
    /// exist in the catalog are skipped, so a deleted preset never leaves a dangling match.</summary>
    public static MediaPresetMatch Resolve(
        MediaFamily family,
        string? mediaId,
        UserMediaPresetStore? userStore,
        Func<string, bool> presetExists)
    {
        ArgumentNullException.ThrowIfNull(presetExists);
        string key = MediaIdentity.Normalize(mediaId);
        if (family == MediaFamily.Unknown || key.Length == 0)
        {
            return MediaPresetMatch.NoMatch;
        }

        UserMediaMapping? saved = userStore?.Find(family, key);
        if (saved is not null && presetExists(saved.PresetId))
        {
            return new(MediaMatchKind.UserSaved, saved.PresetId, [], key);
        }

        foreach (MediaPresetRule rule in BuiltInMediaRules.All)
        {
            if (rule.Family != family
                || !key.Contains(MediaIdentity.Normalize(rule.IdContains), StringComparison.Ordinal))
            {
                continue;
            }

            string[] existing = rule.PresetIds.Where(presetExists).ToArray();
            if (existing.Length > 0)
            {
                return new(MediaMatchKind.BuiltIn, existing[0], existing[1..], rule.Name);
            }
        }

        return MediaPresetMatch.NoMatch;
    }
}
