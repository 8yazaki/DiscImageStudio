namespace DiscImageStudio.Core.Media;

/// <summary>One preset the easy-mode flow may pick. <see cref="IsStandard"/> marks the
/// uncalibrated, nominal preset of a family — the fallback when a disc model is unknown.</summary>
public sealed record EasyPresetOption(
    string Id,
    string DisplayName,
    MediaFamily Family,
    long Sectors,
    bool IsStandard);

/// <summary>What the drive reported about the inserted disc.</summary>
public sealed record EasyMediaState(
    bool HasDisc,
    bool IsBlank,
    MediaFamily Family,
    long? FreeSectors,
    string? MediaId);

public enum EasyNoticeKind
{
    NoDisc,
    NotBlank,
    UnknownMedia,
    UsingSavedCalibration,
    UsingBuiltInPreset,
    Uncalibrated,
    ChosenManually,
    NoPresetForFamily,
    FamilyMismatch,
    CapacityShort,
}

public enum EasyNoticeSeverity
{
    Info,
    Warning,
    Blocker,
}

public sealed record EasyNotice(
    EasyNoticeKind Kind,
    EasyNoticeSeverity Severity,
    string? Text = null,
    long? Number = null);

public sealed record EasyPlan(
    string? PresetId,
    MediaMatchKind Match,
    IReadOnlyList<EasyNotice> Notices,
    bool CanProceed)
{
    public bool NeedsCalibration => Notices.Any(notice => notice.Kind == EasyNoticeKind.Uncalibrated);
}

/// <summary>
/// The decision logic of the easy mode, free of UI and drive access: given the disc that is in
/// the drive and the available presets, which preset applies and what must the user know first.
/// </summary>
public static class EasyModePlanner
{
    /// <summary>IMAPI lets a CD-R run a few sectors past its nominal capacity.</summary>
    public const long CdOverburnToleranceSectors = 3;

    public static EasyPlan Plan(
        EasyMediaState media,
        IReadOnlyList<EasyPresetOption> presets,
        UserMediaPresetStore? userStore,
        string? manualPresetId = null)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(presets);
        List<EasyNotice> notices = [];
        if (!media.HasDisc)
        {
            notices.Add(new(EasyNoticeKind.NoDisc, EasyNoticeSeverity.Blocker));
            return new(null, MediaMatchKind.None, notices, false);
        }

        if (!media.IsBlank)
        {
            notices.Add(new(EasyNoticeKind.NotBlank, EasyNoticeSeverity.Blocker));
        }

        if (media.Family == MediaFamily.Unknown)
        {
            notices.Add(new(EasyNoticeKind.UnknownMedia, EasyNoticeSeverity.Warning));
        }

        EasyPresetOption? chosen = null;
        MediaMatchKind kind = MediaMatchKind.None;
        if (manualPresetId is not null)
        {
            chosen = presets.FirstOrDefault(preset => preset.Id == manualPresetId);
            if (chosen is not null)
            {
                notices.Add(new(EasyNoticeKind.ChosenManually, EasyNoticeSeverity.Info, chosen.DisplayName));
            }
        }

        if (chosen is null)
        {
            MediaPresetMatch match = MediaPresetResolver.Resolve(
                media.Family, media.MediaId, userStore, id => presets.Any(preset => preset.Id == id));
            if (match.PresetId is not null)
            {
                chosen = presets.First(preset => preset.Id == match.PresetId);
                kind = match.Kind;
                notices.Add(match.Kind == MediaMatchKind.UserSaved
                    ? new(EasyNoticeKind.UsingSavedCalibration, EasyNoticeSeverity.Info, chosen.DisplayName)
                    : new(EasyNoticeKind.UsingBuiltInPreset, EasyNoticeSeverity.Info, match.Label));
            }
            else
            {
                chosen = presets.FirstOrDefault(preset => preset.IsStandard && preset.Family == media.Family);
                if (chosen is null)
                {
                    notices.Add(new(EasyNoticeKind.NoPresetForFamily, EasyNoticeSeverity.Blocker));
                }
                else
                {
                    notices.Add(new(EasyNoticeKind.Uncalibrated, EasyNoticeSeverity.Warning, chosen.DisplayName));
                }
            }
        }

        if (chosen is not null)
        {
            if (media.Family != MediaFamily.Unknown && chosen.Family != media.Family)
            {
                notices.Add(new(EasyNoticeKind.FamilyMismatch, EasyNoticeSeverity.Blocker, chosen.DisplayName));
            }

            long tolerance = media.Family == MediaFamily.Cd ? CdOverburnToleranceSectors : 0;
            if (media.FreeSectors is long free && chosen.Sectors > free + tolerance)
            {
                notices.Add(new(EasyNoticeKind.CapacityShort, EasyNoticeSeverity.Blocker,
                    chosen.DisplayName, chosen.Sectors - free));
            }
        }

        bool canProceed = chosen is not null
            && notices.All(notice => notice.Severity != EasyNoticeSeverity.Blocker);
        return new(chosen?.Id, kind, notices, canProceed);
    }
}
