using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Burning;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Media;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ViewModels;

/// <summary>What the easy mode hands back: the project to create, the picture to draw on it and
/// the recorder to preselect on the burn page.</summary>
public sealed record EasyModeResult(
    ProjectDialogResult Project,
    string ImagePath,
    string? DeviceId,
    MeasurementContext? Measurement = null);

/// <summary>The disc model being measured: once the calibration is applied, the result is saved as
/// a preset and paired with this model so the next disc of the kind needs no measuring.</summary>
public sealed record MeasurementContext(MediaFamily Family, string Identity, string? DriveName);

/// <summary>One row of the preset list. A null id is the automatic choice.</summary>
public sealed record EasyChoice(string? PresetId, string Title);

public sealed record EasyNoticeItem(string Text, EasyNoticeSeverity Severity)
{
    public string Glyph => Severity switch
    {
        EasyNoticeSeverity.Blocker => "✕",
        EasyNoticeSeverity.Warning => "!",
        _ => "✓",
    };
}

/// <summary>
/// The guided flow: read the disc in the drive, pick the right measured preset for it, take one
/// picture, and create the project the burn page then writes. All decisions are made by
/// <see cref="EasyModePlanner"/>; this class gathers the inputs and shows the outcome.
/// </summary>
public sealed class EasyModeViewModel : ObservableObject
{
    private const string StandardCdId = "cd-80";
    private const string StandardDvdId = "dvd-5-120mm";
    private const string StandardBluRayId = "bd-r-sl-25gb";

    private readonly IOpticalDiscBurner _burner;
    private readonly IOpticalMediaInspector? _inspector;
    private readonly UserMediaPresetStore _store;
    private readonly string _saveDirectory;
    private readonly List<EasyPresetOption> _options = [];
    private readonly Dictionary<string, ProjectDialogResult> _geometry = new(StringComparer.Ordinal);
    private IReadOnlyList<OpticalBurnDevice> _devices = [];
    private OpticalBurnDevice? _selectedDevice;
    private EasyMediaState _media = new(false, false, MediaFamily.Unknown, null, null);
    private OpticalMediaInfo _mediaInfo = OpticalMediaInfo.Empty;
    private IReadOnlyList<EasyChoice> _choices = [];
    private EasyChoice? _selectedChoice;
    private string? _manualPresetId;
    private EasyPlan? _plan;
    private string _statusText = string.Empty;
    private string _mediaSummary = string.Empty;
    private string _imagePath = string.Empty;
    private string _projectName = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _rememberChoice = true;
    private bool _updatingChoices;

    public EasyModeViewModel(
        IOpticalDiscBurner burner,
        IOpticalMediaInspector? inspector,
        DiscParametersState state,
        UserMediaPresetStore store,
        string defaultProjectDirectory)
    {
        ArgumentNullException.ThrowIfNull(burner);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(store);
        _burner = burner;
        _inspector = inspector;
        _store = store;
        _saveDirectory = string.IsNullOrWhiteSpace(defaultProjectDirectory)
            ? ProjectJsonStore.GetDefaultProjectsDirectory()
            : defaultProjectDirectory;
        BuildOptions(state);
        Notices = [];
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync, () => !IsBusy);
        InspectCommand = new AsyncRelayCommand(InspectAsync, () => !IsBusy && SelectedDevice is not null);
        BrowseImageCommand = new RelayCommand(BrowseImage, () => !IsBusy);
        CreateCommand = new RelayCommand(Confirm, CanCreate);
        MeasureCommand = new RelayCommand(ConfirmMeasure, CanMeasure);
        Recompute();
    }

    // ---- Texts (computed, so a window opened under any language is self-consistent) ----

    public string Title => L.T("简单模式");
    public string Intro => L.T("插入空白光盘并选择一张图片，软件会自动选好参数并建好工程。");
    public string DiscSectionTitle => L.T("光盘");
    public string ImageSectionTitle => L.T("图片");
    public string RecorderLabel => L.T("刻录机");
    public string InspectLabel => L.T("检查光盘");
    public string PresetLabel => L.T("使用的预设");
    public string BrowseLabel => L.T("选择图片…");
    public string ProjectNameLabel => L.T("工程名称");
    public string RememberLabel => L.T("下次遇到这种光盘时自动使用此预设");
    public string CreateLabel => L.T("创建工程并前往刻录");
    public string CancelLabel => L.T("取消");
    public string MeasureLabel => L.T("先测量这种光盘（推荐）");
    public string MeasureHint => L.T("烧一张标定盘并拍照，算出这种光盘的参数。结果会保存，以后这种光盘直接选图片就能烧。");

    // ---- State ----

    public ObservableCollection<EasyNoticeItem> Notices { get; }

    public IReadOnlyList<OpticalBurnDevice> Devices
    {
        get => _devices;
        private set => SetProperty(ref _devices, value);
    }

    public OpticalBurnDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                InspectCommand.NotifyCanExecuteChanged();
                if (value is not null)
                {
                    _ = InspectAsync();
                }
            }
        }
    }

    public IReadOnlyList<EasyChoice> Choices
    {
        get => _choices;
        private set => SetProperty(ref _choices, value);
    }

    public EasyChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (_updatingChoices || !SetProperty(ref _selectedChoice, value) || value is null)
            {
                return;
            }

            _manualPresetId = value.PresetId;
            Recompute();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string MediaSummary
    {
        get => _mediaSummary;
        private set => SetProperty(ref _mediaSummary, value);
    }

    public string ImagePath
    {
        get => _imagePath;
        private set
        {
            if (SetProperty(ref _imagePath, value))
            {
                OnPropertyChanged(nameof(ImageDisplay));
                CreateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ImageDisplay => ImagePath.Length == 0 ? L.T("尚未选择图片") : ImagePath;

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool RememberChoice
    {
        get => _rememberChoice;
        set => SetProperty(ref _rememberChoice, value);
    }

    /// <summary>The remember box only matters when the user picked the preset themselves for a
    /// disc whose identity is known.</summary>
    public bool CanRemember => _manualPresetId is not null
        && MediaIdKey().Length > 0
        && _media.Family != MediaFamily.Unknown;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshDevicesCommand.NotifyCanExecuteChanged();
                InspectCommand.NotifyCanExecuteChanged();
                BrowseImageCommand.NotifyCanExecuteChanged();
                CreateCommand.NotifyCanExecuteChanged();
                MeasureCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshDevicesCommand { get; }

    public IAsyncRelayCommand InspectCommand { get; }

    public IRelayCommand BrowseImageCommand { get; }

    public IRelayCommand CreateCommand { get; }

    public IRelayCommand MeasureCommand { get; }

    /// <summary>Measuring is offered only for a blank disc of a known model that has no
    /// calibrated preset yet: the result has to be paired with something.</summary>
    public bool CanOfferMeasure => _plan is { CanProceed: true, NeedsCalibration: true } && MediaIdKey().Length > 0;

    /// <summary>Installed by the window: shows the image picker and returns a path or null.</summary>
    internal Func<string?>? PickImage { get; set; }

    /// <summary>Installed by the window: closes the dialog with a confirmed result.</summary>
    internal Action? CloseWithConfirm { get; set; }

    public EasyModeResult? Result { get; private set; }

    public EasyPlan? Plan => _plan;

    // ---- Flow ----

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        IsBusy = true;
        StatusText = L.T("正在检测刻录机…");
        try
        {
            IReadOnlyList<OpticalBurnDevice> devices = await _burner.GetDevicesAsync();
            Devices = devices;
            if (devices.Count == 0)
            {
                _selectedDevice = null;
                OnPropertyChanged(nameof(SelectedDevice));
                StatusText = L.T("未检测到支持 IMAPI2 的 CD/DVD/BD 刻录机。");
                Recompute();
                return;
            }

            StatusText = string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Devices = [];
            StatusText = L.T("检测刻录机失败：") + exception.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // Selecting the first recorder reads its disc through the property setter.
        SelectedDevice = Devices[0];
    }

    private async Task InspectAsync()
    {
        if (SelectedDevice is not OpticalBurnDevice device || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("正在读取光盘信息…");
        OpticalMediaInfo info = OpticalMediaInfo.Empty;
        try
        {
            info = _inspector is null
                ? OpticalMediaInfo.Empty
                : await _inspector.InspectMediaAsync(device.Id);
            StatusText = string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = L.T("读取光盘信息失败：") + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }

        _mediaInfo = info;
        string? identity = info.Structure is null
            ? null
            : MediaIdentity.FromStructure(ToStructureKind(info.StructureKind), info.Structure);
        _media = new(info.HasDisc, info.IsBlank, ToFamily(info.Family), info.FreeSectors, identity);
        _manualPresetId = null;
        Recompute();
    }

    private void BrowseImage()
    {
        string? picked = PickImage?.Invoke();
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        ImagePath = picked;
        if (ProjectName.Trim().Length == 0)
        {
            ProjectName = Path.GetFileNameWithoutExtension(picked);
        }

        ErrorMessage = string.Empty;
    }

    private bool CanMeasure() => !IsBusy && CanOfferMeasure;

    private void ConfirmMeasure()
    {
        try
        {
            EasyPlan plan = _plan ?? throw new InvalidOperationException(L.T("请先检查光盘。"));
            if (!CanOfferMeasure || plan.PresetId is null
                || !_geometry.TryGetValue(plan.PresetId, out ProjectDialogResult? template))
            {
                throw new InvalidOperationException(L.T("请先检查光盘。"));
            }

            string identity = MediaIdKey();
            string name = L.F($"校准-{identity}");
            string savePath = Path.Combine(_saveDirectory, SanitizeFileName(name) + ProjectJsonStore.FileExtension);
            Result = new(template with { Name = name, SavePath = savePath }, string.Empty, SelectedDevice?.Id,
                new MeasurementContext(_media.Family, identity, SelectedDevice?.DisplayName));
            ErrorMessage = string.Empty;
            CloseWithConfirm?.Invoke();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private bool CanCreate() => !IsBusy && _plan is { CanProceed: true }
        && ImagePath.Length > 0 && ProjectName.Trim().Length > 0;

    private void Confirm()
    {
        try
        {
            EasyPlan plan = _plan ?? throw new InvalidOperationException(L.T("请先检查光盘。"));
            if (plan.PresetId is null || !_geometry.TryGetValue(plan.PresetId, out ProjectDialogResult? template))
            {
                throw new InvalidOperationException(L.T("请先检查光盘。"));
            }

            if (ImagePath.Length == 0 || !File.Exists(ImagePath))
            {
                throw new FileNotFoundException(L.T("请选择一张图片。"));
            }

            string name = ProjectName.Trim();
            if (name.Length == 0)
            {
                throw new ArgumentException(L.T("请填写工程名。"));
            }

            string savePath = Path.Combine(_saveDirectory, SanitizeFileName(name) + ProjectJsonStore.FileExtension);
            RememberPairing(plan);
            Result = new(template with { Name = name, SavePath = savePath }, ImagePath, SelectedDevice?.Id);
            ErrorMessage = string.Empty;
            CloseWithConfirm?.Invoke();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private void RememberPairing(EasyPlan plan)
    {
        // Only a preset the user chose by hand is worth remembering: automatic matches are
        // already recognized next time, and the uncalibrated standard preset is not a calibration.
        if (!RememberChoice || !CanRemember || plan.PresetId is null || _manualPresetId != plan.PresetId)
        {
            return;
        }

        try
        {
            _store.Remember(new(MediaIdKey(), _media.Family, plan.PresetId, SelectedDevice?.DisplayName,
                DateTimeOffset.Now));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException)
        {
            // Not being able to remember must never stop the project from being created.
        }
    }

    // ---- Presentation ----

    private void Recompute()
    {
        List<EasyPresetOption> relevant = _options
            .Where(option => _media.Family == MediaFamily.Unknown || option.Family == _media.Family)
            .ToList();
        EasyPlan plan = EasyModePlanner.Plan(_media, _options, _store, _manualPresetId);
        _plan = plan;

        _updatingChoices = true;
        try
        {
            List<EasyChoice> choices = [new(null, L.T("自动（推荐）"))];
            choices.AddRange(relevant.Select(option => new EasyChoice(option.Id, option.DisplayName)));
            Choices = choices;
            SetProperty(ref _selectedChoice, choices.FirstOrDefault(choice => choice.PresetId == _manualPresetId)
                ?? choices[0], nameof(SelectedChoice));
        }
        finally
        {
            _updatingChoices = false;
        }

        Notices.Clear();
        foreach (EasyNotice notice in plan.Notices)
        {
            Notices.Add(new(Describe(notice), notice.Severity));
        }

        MediaSummary = DescribeMedia();
        OnPropertyChanged(nameof(CanRemember));
        OnPropertyChanged(nameof(CanOfferMeasure));
        CreateCommand.NotifyCanExecuteChanged();
        MeasureCommand.NotifyCanExecuteChanged();
    }

    private string DescribeMedia()
    {
        if (!_mediaInfo.HasDisc)
        {
            return string.Empty;
        }

        List<string> lines =
        [
            L.F($"光盘类型：{_mediaInfo.TypeName}"),
            _mediaInfo.IsBlank ? L.T("状态：空白") : L.T("状态：已写入数据"),
        ];
        if (_mediaInfo.FreeSectors is long free)
        {
            lines.Add(L.F($"可用空间：{free:N0} 扇区"));
        }

        lines.Add(_media.MediaId is { } id ? L.F($"盘片识别码：{id}") : L.T("盘片识别码：无法读取"));
        return string.Join("\n", lines);
    }

    private static string Describe(EasyNotice notice) => notice.Kind switch
    {
        EasyNoticeKind.NoDisc => L.T("刻录机里没有光盘。请放入空白光盘，然后点“检查光盘”。"),
        EasyNoticeKind.NotBlank => L.T("这张光盘不是空白盘。刻录需要未使用过的光盘。"),
        EasyNoticeKind.UnknownMedia => L.T("无法判断光盘类型。请在下方手动选择预设。"),
        EasyNoticeKind.UsingSavedCalibration => L.F($"使用已保存的校准预设：{notice.Text}"),
        EasyNoticeKind.UsingBuiltInPreset => L.F($"已识别这种光盘，使用实测预设：{notice.Text}"),
        EasyNoticeKind.Uncalibrated => L.F($"这种光盘还没有校准。直接用标准预设（{notice.Text}）烧的话，图案很可能错位或发虚。建议先点“先测量这种光盘”，只需做一次。"),
        EasyNoticeKind.ChosenManually => L.F($"使用你选择的预设：{notice.Text}"),
        EasyNoticeKind.NoPresetForFamily => L.T("没有适用于这种光盘的预设。"),
        EasyNoticeKind.FamilyMismatch => L.F($"所选预设（{notice.Text}）与这张光盘的类型不符。"),
        EasyNoticeKind.CapacityShort => L.F($"预设（{notice.Text}）比光盘的可用空间多 {notice.Number:N0} 个扇区，无法写入。请选择其他预设。"),
        _ => string.Empty,
    };

    // ---- Presets ----

    private void BuildOptions(DiscParametersState state)
    {
        foreach (DiscImageStudio.Cd.CdDiscPreset preset in state.CdPresets.Where(p => !p.IsCustom))
        {
            Add(preset.Id, preset.DisplayName, MediaFamily.Cd, preset.Sectors, StandardCdId,
                new(preset.DisplayName, OpticalDiscFamily.CompactDisc, preset.InnerRadiusMm, preset.OuterRadiusMm,
                    preset.Sectors, 0, 0, 0, string.Empty));
        }

        foreach (DiscImageStudio.Dvd.DvdDiscPreset preset in state.DvdPresets.Where(p => !p.IsCustom))
        {
            Add(preset.Id, preset.DisplayName, MediaFamily.Dvd, preset.TotalSectors, StandardDvdId,
                new(preset.DisplayName, OpticalDiscFamily.Dvd, preset.InnerRadiusMm, preset.OuterRadiusMm,
                    preset.TotalSectors, preset.PitchLinear, preset.PitchQuadratic, preset.PitchCubic, string.Empty));
        }

        foreach (DiscImageStudio.BluRay.BluRayDiscPreset preset in state.BluRayPresets.Where(p => !p.IsCustom))
        {
            DiscImageStudio.BluRay.BluRayDiscParameters geometry = preset.CreateParameters();
            Add(preset.Id, preset.DisplayName, MediaFamily.BluRay, geometry.TotalSectors, StandardBluRayId,
                new(preset.DisplayName, OpticalDiscFamily.BluRay, geometry.InnerRadiusMm, geometry.OuterRadiusMm,
                    geometry.TotalSectors, 0, 0, 0, string.Empty, BluRayInvertMapping: preset.InvertMapping));
        }

        void Add(string id, string name, MediaFamily family, long sectors, string standardId,
            ProjectDialogResult template)
        {
            _options.Add(new(id, name, family, sectors, id == standardId));
            _geometry[id] = template;
        }
    }

    private string MediaIdKey() => MediaIdentity.Normalize(_media.MediaId);

    private static MediaFamily ToFamily(OpticalMediaFamily family) => family switch
    {
        OpticalMediaFamily.Cd => MediaFamily.Cd,
        OpticalMediaFamily.Dvd => MediaFamily.Dvd,
        OpticalMediaFamily.BluRay => MediaFamily.BluRay,
        _ => MediaFamily.Unknown,
    };

    private static MediaStructureKind ToStructureKind(OpticalMediaStructure kind) => kind switch
    {
        OpticalMediaStructure.DvdPreRecorded => MediaStructureKind.DvdPreRecorded,
        OpticalMediaStructure.DvdAdip => MediaStructureKind.DvdAdip,
        OpticalMediaStructure.CdAtip => MediaStructureKind.CdAtip,
        _ => MediaStructureKind.None,
    };

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return cleaned.Length == 0 ? L.T("工程") : cleaned;
    }
}
