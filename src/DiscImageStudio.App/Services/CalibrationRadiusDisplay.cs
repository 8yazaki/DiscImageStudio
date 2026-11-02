using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Services;

internal static class CalibrationRadiusDisplay
{
    internal static string Format(CalibrationTarget target)
    {
        CalibrationParameters p = target.Parameters;
        return L.F("完整数据区半径（创建图案时）：{0}–{1} mm", ParameterNumberText.ForInput(p.InnerRadiusMm), ParameterNumberText.ForInput(p.OuterRadiusMm));
    }
}
