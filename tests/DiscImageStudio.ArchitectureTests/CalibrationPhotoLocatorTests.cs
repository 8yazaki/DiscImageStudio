using DiscImageStudio.Core.Calibration;

/// <summary>Disc and hole detection on a synthetic oblique photo with a cluttered background.</summary>
internal static class CalibrationPhotoLocatorTests
{
    internal static void Run()
    {
        const int width = 900, height = 700;
        const double cx = 470, cy = 340, a = 270, b = 235, tilt = 0.17;
        double cos = Math.Cos(tilt), sin = Math.Sin(tilt);
        byte[] gray = new byte[width * height];
        Random random = new(5);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = x - cx, dy = y - cy;
                double u = (dx * cos + dy * sin) / a, v = (-dx * sin + dy * cos) / b;
                double d = Math.Sqrt(u * u + v * v);
                double value = 95 + 6 * Math.Sin(x / 11.0) + random.NextDouble() * 10; // wall texture
                if (d < 1)
                {
                    // rainbow-lit disc, brighter than the wall on one side and darker on the other
                    value = 170 + 60 * Math.Sin(3 * Math.Atan2(v, u)) + random.NextDouble() * 10;
                    if (d < 0.125 * 1.15)
                    {
                        value = d < 0.125 ? 235 : 40; // hub ring and hole
                    }
                }

                // a hand covers part of the rim
                if (x > 700 && y > 560)
                {
                    value = 90 + random.NextDouble() * 20;
                }

                gray[y * width + x] = (byte)Math.Clamp(value, 0, 255);
            }
        }

        CalibrationPhotoLocation? found = CalibrationPhotoLocator.Locate(gray, width, height);
        Require(found is not null, "disc found");
        double worst = 0;
        int checkedPoints = 0;
        foreach (CalibrationPoint p in found!.Outer)
        {
            double dx = p.X - cx, dy = p.Y - cy;
            double u = (dx * cos + dy * sin) / a, v = (-dx * sin + dy * cos) / b;
            worst = Math.Max(worst, Math.Abs(Math.Sqrt(u * u + v * v) - 1));
            checkedPoints++;
        }

        Require(checkedPoints == 8 && worst < 0.04, $"outer points sit on the rim (worst {worst:F3})");
        double holeX = found.Hole.Average(p => p.X), holeY = found.Hole.Average(p => p.Y);
        Require(Math.Abs(holeX - cx) < 25 && Math.Abs(holeY - cy) < 25, $"hole centre found ({holeX:F0},{holeY:F0})");
        double holeSize = found.Hole.Max(p => p.X) - found.Hole.Min(p => p.X);
        Require(holeSize > 2 * 0.125 * a * 0.7 && holeSize < 2 * 0.125 * a * 1.6, $"hole size plausible ({holeSize:F0})");
        Console.WriteLine("calibration-photo-locator: disc outline and hole found on a cluttered synthetic photo");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Photo locator: {name}.");
        }
    }
}
