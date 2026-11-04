using DiscImageStudio.Core.Calibration;

/// <summary>Ring measurement on a synthetic photo: rings drawn where an off-nominal disc would put
/// them, under uneven, sign-flipping brightness and noise like a rainbow-lit dye disc.</summary>
internal static class CalibrationRingFitterTests
{
    internal static void Run()
    {
        TestRecoversRadii(0.18, -0.11, 1);
        TestRecoversRadii(-1.7, -0.9, 4);
        TestRecoversRadii(-0.42, 0.37, 2);
        TestRecoversRadii(0.0, 0.0, 3);
        TestNoiseGivesLowConfidence();
        Console.WriteLine("calibration-ring-fitter: radii recovered under uneven lighting; plain noise scores low");
    }

    private static CalibrationParameters Generated() => new(CalibrationDiscKind.Dvd, 24, 58, 2297888, 133.3);

    private static void TestRecoversRadii(double dInner, double dOuter, int seed)
    {
        CalibrationParameters generated = Generated();
        CalibrationParameters actual = generated with
        {
            InnerRadiusMm = generated.InnerRadiusMm + dInner,
            OuterRadiusMm = generated.OuterRadiusMm + dOuter,
        };
        CalibrationTarget target = CalibrationTarget.Create(generated);
        double[] landed = LandedRadii(target, generated, actual);
        Random random = new(seed);
        double[] noiseTable = Enumerable.Range(0, 4096).Select(_ => random.NextDouble() - 0.5).ToArray();

        double? Sample(double x, double y)
        {
            double radius = Math.Sqrt(x * x + y * y), angle = Math.Atan2(y, x);
            double ring = 0;
            foreach (double centre in landed)
            {
                double d = (radius - centre) / 0.24;
                ring += Math.Exp(-0.5 * d * d);
            }

            // Brightness and the sign of the ring contrast change with direction, like a rainbow.
            double polarity = Math.Sin(2 * angle + 0.7) >= 0 ? 1 : -1;
            double level = 0.55 + 0.45 * Math.Sin(3 * angle);
            int cell = (int)(radius * 50) * 31 + (int)((angle + Math.PI) * 40);
            double noise = noiseTable[Math.Abs(cell) % noiseTable.Length] * 30;
            return 128 + 40 * Math.Sin(radius * 9 + 3 * angle) * level + polarity * level * 60 * ring + noise;
        }

        CalibrationRingFitResult fit = CalibrationRingFitter.Fit(target, Sample);
        Require(Math.Abs(fit.Parameters.InnerRadiusMm - actual.InnerRadiusMm) < 0.03,
            $"inner radius recovered for shift {dInner:+0.00;-0.00} (got {fit.Parameters.InnerRadiusMm:F3})");
        Require(Math.Abs(fit.Parameters.OuterRadiusMm - actual.OuterRadiusMm) < 0.03,
            $"outer radius recovered for shift {dOuter:+0.00;-0.00} (got {fit.Parameters.OuterRadiusMm:F3})");
        Require(fit.Confidence > 3, $"clear rings give high confidence (got {fit.Confidence:F1})");
    }

    private static void TestNoiseGivesLowConfidence()
    {
        CalibrationParameters generated = Generated();
        CalibrationTarget target = CalibrationTarget.Create(generated);
        Random random = new(9);
        double[] table = Enumerable.Range(0, 8192).Select(_ => random.NextDouble() * 255).ToArray();
        double? Sample(double x, double y)
            => table[Math.Abs((int)(Math.Sqrt(x * x + y * y) * 53) * 17 + (int)((Math.Atan2(y, x) + Math.PI) * 70)) % table.Length];
        CalibrationRingFitResult fit = CalibrationRingFitter.Fit(target, Sample);
        Require(fit.Confidence < 3.3, $"no rings, low confidence (got {fit.Confidence:F1})");
    }

    private static double[] LandedRadii(CalibrationTarget target, CalibrationParameters generated, CalibrationParameters actual)
    {
        Func<CalibrationPoint, CalibrationPoint> map = target.CreateMapping(actual);
        double scale = CalibrationReferencePattern.MillimetresPerPixel(generated.OuterRadiusMm, generated.Kind);
        return CalibrationReferencePattern.RingRadiiPixels
            .Select(pixels => pixels * scale)
            .Where(radius => radius >= generated.InnerRadiusMm + 0.3 && radius <= generated.OuterRadiusMm - 0.3)
            .Select(radius => { CalibrationPoint p = map(new CalibrationPoint(radius, 0)); return Math.Sqrt(p.X * p.X + p.Y * p.Y); })
            .ToArray();
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Ring fitter: {name}.");
        }
    }
}
