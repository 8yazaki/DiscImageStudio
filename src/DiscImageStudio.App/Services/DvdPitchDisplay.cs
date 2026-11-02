using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Services;

internal static class DvdPitchDisplay
{
    internal static string Format(CalibrationParameters parameters)
    {
        parameters.Validate();
        var geometry = DvdTrackGeometry.Create(parameters.InnerRadiusMm, parameters.OuterRadiusMm,
            parameters.TrackLengthMm, parameters.PitchLinear, parameters.PitchQuadratic, parameters.PitchCubic);
        double scale = geometry.TrackPitchMm * 1000;
        string Number(double value) => ParameterNumberText.ForSummary(value, 10);
        return L.F("轨距 p(x) = {0} + ({1})x + ({2})x² + ({3})x³ μm", Number(scale), Number(scale * parameters.PitchLinear), Number(scale * parameters.PitchQuadratic), Number(scale * parameters.PitchCubic));
    }
}
