namespace DiscImageStudio.Core.Calibration;

/// <summary>What the ring measurement found: the inner and outer radius the burned rings imply,
/// and how clearly the rings stood out from the rest of the photo.</summary>
public sealed record CalibrationRingFitResult(
    CalibrationParameters Parameters,
    int RingCount,
    double Confidence,
    double InnerShiftMm,
    double OuterShiftMm);

/// <summary>
/// Measures where the burned ring guides really landed and derives the inner and outer radius of
/// the track model from that. Rings are full circles, so unlike the cross arms they stay visible
/// when the radii are off by a fraction of a millimetre: that makes them the first, coarse step
/// for a disc nobody has calibrated. The photo is only read through a sampler of the rectified
/// disc (millimetres from the disc centre, Y down), so no image library is involved.
/// </summary>
public static class CalibrationRingFitter
{
    public const double DefaultSearchMm = 2.5;
    public const double MaxSearchMm = 3.0;
    /// <summary>Largest shift of a single ring; beyond half the ring spacing it could be mistaken for the next ring.</summary>
    private const double MaxRingShiftMm = 1.9;
    private const int AngleSamples = 360;
    private const double ProfileStepMm = 0.02;
    private const double CoreHalfWidthMm = 0.2;
    private const double FlankNearMm = 0.35;
    private const double FlankFarMm = 0.8;
    private const double RingMarginMm = 0.3;
    private const double CoarseStepMm = 0.1;
    private const double FineStepMm = 0.01;

    /// <summary>Radial score curve: the mean normalized ring contrast of the photo at each radius.</summary>
    internal sealed record RadialScore(double StartMm, double StepMm, double[] Values)
    {
        public double At(double radiusMm)
        {
            double position = (radiusMm - StartMm) / StepMm;
            int index = (int)Math.Floor(position);
            if (index < 0 || index + 1 >= Values.Length)
            {
                return 0;
            }

            double fraction = position - index;
            return Values[index] * (1 - fraction) + Values[index + 1] * fraction;
        }
    }

    public static CalibrationRingFitResult Fit(
        CalibrationTarget target,
        Func<double, double, double?> sampleGray,
        double searchMm = DefaultSearchMm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sampleGray);
        if (!double.IsFinite(searchMm) || searchMm <= 0 || searchMm > MaxSearchMm)
        {
                        throw new ArgumentOutOfRangeException(nameof(searchMm));
        }

        CalibrationParameters generated = target.Parameters;
        double[] ringRadii = RingRadiiMm(generated);
        if (ringRadii.Length < 3)
        {
            throw new InvalidOperationException("标定图案的圆环太少，无法测量。");
        }

        RadialScore score = BuildRadialScore(generated, sampleGray, searchMm, cancellationToken);

        (double Inner, double Outer, double Value) best = (generated.InnerRadiusMm, generated.OuterRadiusMm, double.NegativeInfinity);
        List<double> coarseValues = [];
        for (double dInner = -searchMm; dInner <= searchMm + 1e-9; dInner += CoarseStepMm)
        {
            for (double dOuter = -searchMm; dOuter <= searchMm + 1e-9; dOuter += CoarseStepMm)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double value = Evaluate(target, generated, ringRadii, score,
                    generated.InnerRadiusMm + dInner, generated.OuterRadiusMm + dOuter);
                if (double.IsFinite(value))
                {
                    coarseValues.Add(value);
                    if (value > best.Value)
                    {
                        best = (generated.InnerRadiusMm + dInner, generated.OuterRadiusMm + dOuter, value);
                    }
                }
            }
        }

        if (coarseValues.Count == 0)
        {
            throw new InvalidOperationException("在搜索范围内没有可用的半径组合。");
        }

        // The coarse grid is 0.1 mm; the ring is ~0.5 mm wide, so the true optimum lies within one
        // coarse step of the best cell. Refine there.
        (double Inner, double Outer, double Value) refined = best;
        for (double dInner = -CoarseStepMm; dInner <= CoarseStepMm + 1e-9; dInner += FineStepMm)
        {
            for (double dOuter = -CoarseStepMm; dOuter <= CoarseStepMm + 1e-9; dOuter += FineStepMm)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double value = Evaluate(target, generated, ringRadii, score, best.Inner + dInner, best.Outer + dOuter);
                if (double.IsFinite(value) && value > refined.Value)
                {
                    refined = (best.Inner + dInner, best.Outer + dOuter, value);
                }
            }
        }

        double mean = coarseValues.Average();
        double deviation = Math.Sqrt(coarseValues.Sum(value => (value - mean) * (value - mean)) / coarseValues.Count);
        double confidence = deviation > 0 ? (refined.Value - mean) / deviation : 0;
        return new CalibrationRingFitResult(
            generated with { InnerRadiusMm = refined.Inner, OuterRadiusMm = refined.Outer },
            ringRadii.Length,
            confidence,
            refined.Inner - generated.InnerRadiusMm,
            refined.Outer - generated.OuterRadiusMm);
    }

    /// <summary>Ring radii of the reference pattern in the generation geometry, limited to rings that
    /// are fully inside the written annulus.</summary>
    internal static double[] RingRadiiMm(CalibrationParameters generated)
    {
        double scale = CalibrationReferencePattern.MillimetresPerPixel(generated.OuterRadiusMm, generated.Kind);
        return CalibrationReferencePattern.RingRadiiPixels
            .Select(pixels => pixels * scale)
            .Where(radius => radius >= generated.InnerRadiusMm + RingMarginMm
                && radius <= generated.OuterRadiusMm - RingMarginMm)
            .ToArray();
    }

    private static double Evaluate(
        CalibrationTarget target,
        CalibrationParameters generated,
        double[] ringRadii,
        RadialScore score,
        double inner,
        double outer)
    {
        if (!(inner > 0) || !(outer > inner))
        {
            return double.NaN;
        }

        try
        {
            Func<CalibrationPoint, CalibrationPoint> map = target.CreateMapping(
                generated with { InnerRadiusMm = inner, OuterRadiusMm = outer });
            double total = 0;
            foreach (double radius in ringRadii)
            {
                CalibrationPoint landed = map(new CalibrationPoint(radius, 0));
                double landedRadius = Math.Sqrt(landed.X * landed.X + landed.Y * landed.Y);
                if (Math.Abs(landedRadius - radius) > MaxRingShiftMm)
                {
                    // Rings are ~3.9 mm apart: a larger shift would lock onto the neighbouring ring.
                    return double.NaN;
                }

                total += score.At(landedRadius);
            }

            return total;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return double.NaN;
        }
    }

    private static RadialScore BuildRadialScore(
        CalibrationParameters generated,
        Func<double, double, double?> sampleGray,
        double searchMm,
        CancellationToken cancellationToken)
    {
        double start = Math.Max(1, generated.InnerRadiusMm - searchMm - FlankFarMm - 0.1);
        double end = generated.OuterRadiusMm + searchMm + FlankFarMm + 0.1;
        int count = (int)Math.Ceiling((end - start) / ProfileStepMm) + 1;
        int core = (int)Math.Round(CoreHalfWidthMm / ProfileStepMm);
        int near = (int)Math.Round(FlankNearMm / ProfileStepMm);
        int far = (int)Math.Round(FlankFarMm / ProfileStepMm);

        double[] sum = new double[count];
        int[] used = new int[count];
        double[] profile = new double[count];
        double[] contrast = new double[count];
        for (int angleIndex = 0; angleIndex < AngleSamples; angleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double angle = 2 * Math.PI * angleIndex / AngleSamples;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            for (int i = 0; i < count; i++)
            {
                double radius = start + i * ProfileStepMm;
                profile[i] = sampleGray(radius * cos, radius * sin) ?? double.NaN;
            }

            // Band-pass response: the mean inside the ring minus the mean of both flanks.
            int valid = 0;
            double energy = 0;
            for (int i = 0; i < count; i++)
            {
                contrast[i] = double.NaN;
                if (i - far < 0 || i + far >= count)
                {
                    continue;
                }

                double centre = Mean(profile, i - core, i + core);
                double left = Mean(profile, i - far, i - near);
                double right = Mean(profile, i + near, i + far);
                double value = centre - (left + right) / 2;
                if (double.IsFinite(value))
                {
                    contrast[i] = Math.Abs(value);
                    energy += contrast[i];
                    valid++;
                }
            }

            if (valid < count / 4 || energy <= 0)
            {
                continue;
            }

            // Bright rainbow sectors would otherwise dominate the average; each direction counts equally.
            double scale = valid / energy;
            for (int i = 0; i < count; i++)
            {
                if (double.IsFinite(contrast[i]))
                {
                    sum[i] += contrast[i] * scale;
                    used[i]++;
                }
            }
        }

        double[] values = new double[count];
        bool any = false;
        for (int i = 0; i < count; i++)
        {
            values[i] = used[i] > 0 ? sum[i] / used[i] : 0;
            any |= used[i] > AngleSamples / 4;
        }

        if (!any)
        {
            throw new InvalidOperationException("照片没有覆盖足够的盘面，无法测量圆环。");
        }

        return new RadialScore(start, ProfileStepMm, values);
    }

    private static double Mean(double[] values, int from, int to)
    {
        double total = 0;
        int count = 0;
        for (int i = from; i <= to; i++)
        {
            double value = values[i];
            if (!double.IsFinite(value))
            {
                return double.NaN;
            }

            total += value;
            count++;
        }

        return count == 0 ? double.NaN : total / count;
    }
}
