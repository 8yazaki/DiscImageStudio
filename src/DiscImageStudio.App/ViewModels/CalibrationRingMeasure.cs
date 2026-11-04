using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Controls;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ViewModels;

/// <summary>
/// The coarse first step for a disc nobody has calibrated: the burned rings are full circles, so they
/// stay visible when the cross arms have wound into an unreadable spiral. Their positions give the
/// inner and outer radius well enough for the arms to show up on the next calibration disc.
/// </summary>
public partial class CalibrationViewModel
{
    private const double MinimumRingConfidence = 3.0;
    private IAsyncRelayCommand? _measureRingsCommand;

    public IAsyncRelayCommand MeasureRingsCommand => _measureRingsCommand ??= new AsyncRelayCommand(MeasureRingsAsync);

    private async Task MeasureRingsAsync()
    {
        if (_target is not { } target || _photoTransform is not { } transform || PhotoImage is not { } image)
        {
            _shell.ShowToast(L.T("无法测量圆环"), L.T("请先导入照片并完成照片矫正。"), ToastKind.Warning);
            return;
        }

        // Pixels are copied on the UI thread; the search itself runs on a worker.
        Func<double, double, double?> sampler = CreateDiscSampler(image, transform);
        await RunAsync(L.T("正在测量圆环位置…"), async token =>
        {
            CalibrationRingFitResult result = await Task.Run(
                () => CalibrationRingFitter.Fit(target, sampler, CalibrationRingFitter.DefaultSearchMm, token), token);
            token.ThrowIfCancellationRequested();
            if (result.Confidence < MinimumRingConfidence)
            {
                _statusMessage = L.F($"没有清楚地找到圆环（可信度 {result.Confidence:0.0}）。请换一张圆环更清楚的照片。");
                _shell.ShowToast(L.T("无法测量圆环"), _statusMessage, ToastKind.Warning);
                return;
            }

            ApplyRadii(result.Parameters);
            _statusMessage = L.F($"已按圆环更新半径：内 {result.Parameters.InnerRadiusMm:0.000} mm，外 {result.Parameters.OuterRadiusMm:0.000} mm（相对原值 {result.InnerShiftMm:+0.000;-0.000} / {result.OuterShiftMm:+0.000;-0.000} mm）。请用新参数重新刻一张标定盘，十字线才会清楚。");
            _shell.AppendLog(L.F($"[{DateTime.Now:HH:mm:ss}] 标定：{_statusMessage}\n"));
            _shell.ShowToast(L.T("已更新半径"), _statusMessage, ToastKind.Success);
            // Rebuilding the target clears the photo step; the photo and its correction stay valid
            // (they do not depend on the radii), so they are put back and the user keeps working.
            PhotoSnapshot snapshot = TakePhotoSnapshot();
            CreateTarget();
            RestorePhotoSnapshot(snapshot);
            if (_shell.ActiveMeasurement is not null)
            {
                _shell.MeasurementRound++;
                PrepareSecondRound();
            }
        });
    }

    private sealed record PhotoSnapshot(
        BitmapSource? Photo, byte[]? Png, IReadOnlyList<CalibrationPhotoBoundaryPoint> Boundary,
        IReadOnlyList<System.Windows.Point[]> Contours, CalibrationPhotoTransform? Transform,
        CalibrationPhotoFitResult? Fit, BitmapSource? Rectified, bool RectificationReady,
        bool Preview, bool PhotoStep, string Summary, IReadOnlyList<CalibrationTracePoint> Trace);

    private PhotoSnapshot TakePhotoSnapshot() => new(
        PhotoImage, _photoPng, PhotoBoundaryPoints, PhotoBoundaryContours, _photoTransform, _rectificationFit,
        RectifiedPhoto, IsRectificationReady, PreviewRectification, IsPhotoStep, RectificationSummary, TracePoints);

    private void RestorePhotoSnapshot(PhotoSnapshot s)
    {
        if (s.Photo is null || s.Transform is null)
        {
            return;
        }

        PhotoImage = s.Photo; _photoPng = s.Png; PhotoBoundaryPoints = s.Boundary; PhotoBoundaryContours = s.Contours;
        _photoTransform = s.Transform; _rectificationFit = s.Fit; RectifiedPhoto = s.Rectified;
        IsRectificationReady = s.RectificationReady; PreviewRectification = s.Preview; IsPhotoStep = s.PhotoStep;
        RectificationSummary = s.Summary; TracePoints = s.Trace;
        RefreshPhotoCommands();
    }

    /// <summary>In a measurement the next disc is burned right away: the new pattern is saved beside the
    /// project and the burn page opens with it selected.</summary>
    private void PrepareSecondRound()
    {
        if (_target is null || string.IsNullOrEmpty(_shell.ProjectFilePath))
        {
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(_shell.ProjectFilePath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(_shell.ProjectFilePath);
            SaveSessionTo(Path.Combine(directory, $"{name}-{_shell.MeasurementRound + 1}.calibration.json"));
            _shell.Burn.UseCalibrationPattern = true;
            _shell.NavigateTo(Workspace.Burn);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(exception);
        }
    }

    private void ApplyRadii(CalibrationParameters p)
    {
        string inner = p.InnerRadiusMm.ToString("G17", CultureInfo.InvariantCulture);
        string outer = p.OuterRadiusMm.ToString("G17", CultureInfo.InvariantCulture);
        switch (p.Kind)
        {
            case CalibrationDiscKind.Cd:
                _state.CdInnerRadius = inner; _state.CdOuterRadius = outer;
                _state.CdActualInnerRadius = inner; _state.CdActualOuterRadius = outer;
                break;
            case CalibrationDiscKind.BluRay:
                _state.BdInnerRadius = inner; _state.BdOuterRadius = outer;
                _state.BdActualInnerRadius = inner; _state.BdActualOuterRadius = outer;
                break;
            default:
                _state.DvdInnerRadius = inner; _state.DvdOuterRadius = outer;
                _state.DvdActualInnerRadius = inner; _state.DvdActualOuterRadius = outer;
                break;
        }
    }

    private static Func<double, double, double?> CreateDiscSampler(BitmapSource image, CalibrationPhotoTransform transform)
    {
        FormatConvertedBitmap gray = new(image, PixelFormats.Gray8, null, 0);
        int width = gray.PixelWidth, height = gray.PixelHeight;
        byte[] pixels = new byte[width * height];
        gray.CopyPixels(pixels, width, 0);
        return (x, y) =>
        {
            CalibrationPoint source = transform.DiscToSource(new CalibrationPoint(x, y));
            double sx = source.X, sy = source.Y;
            if (!double.IsFinite(sx) || !double.IsFinite(sy) || sx < 0 || sy < 0 || sx > width - 1.001 || sy > height - 1.001)
            {
                return null;
            }

            int x0 = (int)sx, y0 = (int)sy;
            double fx = sx - x0, fy = sy - y0;
            double top = pixels[y0 * width + x0] * (1 - fx) + pixels[y0 * width + x0 + 1] * fx;
            double bottom = pixels[(y0 + 1) * width + x0] * (1 - fx) + pixels[(y0 + 1) * width + x0 + 1] * fx;
            return top * (1 - fy) + bottom * fy;
        };
    }
}
