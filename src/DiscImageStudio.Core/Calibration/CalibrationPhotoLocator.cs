namespace DiscImageStudio.Core.Calibration;

/// <summary>Where the disc and its centre hole were found in a photo (pixel coordinates).</summary>
public sealed record CalibrationPhotoLocation(
    IReadOnlyList<CalibrationPoint> Outer,
    IReadOnlyList<CalibrationPoint> Hole);

/// <summary>
/// Finds the outer rim and the centre hole of a disc in a photo, so the control points of the
/// photo correction start on the disc instead of at a generic ellipse. Works on a grey image:
/// a circle vote gives the rough disc, a ring search with a smoothness constraint follows the rim,
/// and an ellipse fit (which discards the stretches where the rim is hidden by a hand or glare)
/// makes the outline. This is a starting guess; the control points can still be dragged.
/// </summary>
public static class CalibrationPhotoLocator
{
    private const int WorkingSize = 640;

    public static CalibrationPhotoLocation? Locate(
        byte[] gray, int width, int height, int pointCount = 8, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gray);
        if (width < 32 || height < 32 || gray.Length < width * height || pointCount < 5)
        {
            return null;
        }

        double scale = Math.Max(1, Math.Max(width, height) / (double)WorkingSize);
        int w = (int)(width / scale), h = (int)(height / scale);
        double[] image = Downscale(gray, width, height, w, h, scale);
        image = Blur(image, w, h, 2);
        (double[] gx, double[] gy, double[] magnitude) = Gradient(image, w, h);

        cancellationToken.ThrowIfCancellationRequested();
        (double cx, double cy, double radius)? rough = FindCircle(gx, gy, magnitude, w, h, 0.22, 0.55, 92, null, cancellationToken);
        if (rough is null)
        {
            return null;
        }

        var (rcx, rcy, rr) = rough.Value;
        // First pass is generous (the circle vote is only roughly right for a tilted disc); the second
        // pass starts from the ellipse found by the first and looks only near its rim.
        Ellipse? outer = Ellipse.Fit(FollowRing(image, w, h, rcx, rcy, rr * 0.55, rr * 1.6, 48, cancellationToken));
        if (outer is not null && outer.IsFinite)
        {
            outer = Ellipse.Fit(FollowRing(image, w, h, outer.CenterX, outer.CenterY,
                outer.MeanRadius * 0.8, outer.MeanRadius * 1.25, 72, cancellationToken)) ?? outer;
        }

        if (outer is null || outer.MeanRadius < Math.Min(w, h) * 0.15 || !outer.IsFinite)
        {
            return null;
        }

        // Centre hole: a small circle near the disc centre; it has the disc's own aspect and tilt.
        double mean = outer.MeanRadius;
        (double cx, double cy, double radius)? hole = FindCircle(
            gx, gy, magnitude, w, h, 0, 0, 85, (outer.CenterX, outer.CenterY, mean * 0.4), cancellationToken, mean * 0.108, mean * 0.138);
        double holeX = hole?.cx ?? outer.CenterX, holeY = hole?.cy ?? outer.CenterY, holeR = hole?.radius ?? mean * 0.125;

        // The hole edge has a chamfer and sits next to the transparent hub, so the strongest circle
        // can be one of the outer edges. Two candidates are tried: the innermost clearly visible edge
        // and the strongest circle. For each, the hole outline is followed (a tilted photo makes it an
        // ellipse of its own) and the size consistent with the rim under the photo correction is taken.
        CalibrationPoint[] outerOnly = outer.Points(pointCount, 1, outer.CenterX, outer.CenterY, scale);
        double innermost = InnermostEdgeRadius(image, w, h, outer, holeX, holeY, mean, holeR);
        double[] factors = [1, 0.98, 1.02, 0.96, 1.04, 0.94, 1.06, 0.92, 1.08, 0.9, 1.1, 0.88, 1.12];
        CalibrationPoint[]? fallbackHole = null;
        foreach (double baseRadius in new[] { innermost, holeR })
        {
            Ellipse? holeEllipse = Ellipse.Fit(FollowRing(image, w, h, holeX, holeY, baseRadius * 0.92, baseRadius * 1.08, 36, cancellationToken));
            if (holeEllipse is not { IsFinite: true } he || he.MeanRadius <= mean * 0.07 || he.MeanRadius >= mean * 0.2
                || Math.Abs(he.CenterX - outer.CenterX) >= mean * 0.25 || Math.Abs(he.CenterY - outer.CenterY) >= mean * 0.25)
            {
                continue;
            }

            fallbackHole ??= he.Points(pointCount, 1, he.CenterX, he.CenterY, scale);
            foreach (double factor in factors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalibrationPoint[] candidate = he.Points(pointCount, factor, he.CenterX, he.CenterY, scale);
                if (CalibrationPhotoRectifier.Fit(outerOnly, candidate, 60, 7.5, cancellationToken).Succeeded)
                {
                    return new CalibrationPhotoLocation(Array.AsReadOnly(outerOnly), Array.AsReadOnly(candidate));
                }
            }
        }

        if (fallbackHole is not null)
        {
            return new CalibrationPhotoLocation(Array.AsReadOnly(outerOnly), Array.AsReadOnly(fallbackHole));
        }

        CalibrationPoint[] outerPoints = outer.Points(pointCount, 1, outer.CenterX, outer.CenterY, scale);
        CalibrationPoint[] holePoints = outer.Points(pointCount, holeR / mean, holeX, holeY, scale);
        return new CalibrationPhotoLocation(Array.AsReadOnly(outerPoints), Array.AsReadOnly(holePoints));
    }

    /// <summary>Radius (as an outer-ellipse-shaped ring) of the innermost edge that is nearly as strong
    /// as the strongest one within the plausible hole size.</summary>
    private static double InnermostEdgeRadius(double[] image, int w, int h, Ellipse outer, double cx, double cy, double mean, double fallback)
    {
        double low = mean * 0.1, high = mean * 0.145;
        int steps = Math.Max(8, (int)((high - low) / 0.5));
        double[] score = new double[steps + 1];
        const int angles = 72;
        for (int i = 0; i <= steps; i++)
        {
            double r = low + (high - low) * i / steps;
            double total = 0;
            for (int a = 0; a < angles; a++)
            {
                double t = 2 * Math.PI * a / angles;
                (double x0, double y0) = outer.Local(t, (r - 1.0) / mean);
                (double x1, double y1) = outer.Local(t, (r + 1.0) / mean);
                total += Math.Abs(Sample(image, w, h, cx + x1, cy + y1) - Sample(image, w, h, cx + x0, cy + y0));
            }

            score[i] = total / angles;
        }

        double max = score.Max();
        if (max <= 0)
        {
            return fallback;
        }

        for (int i = 1; i < steps; i++)
        {
            if (score[i] >= 0.7 * max && score[i] >= score[i - 1] && score[i] >= score[i + 1])
            {
                return low + (high - low) * i / steps;
            }
        }

        return fallback;
    }

    private static double[] Downscale(byte[] gray, int width, int height, int w, int h, double scale)
    {
        double[] result = new double[w * h];
        for (int y = 0; y < h; y++)
        {
            int y0 = (int)(y * scale), y1 = Math.Min(height, Math.Max(y0 + 1, (int)((y + 1) * scale)));
            for (int x = 0; x < w; x++)
            {
                int x0 = (int)(x * scale), x1 = Math.Min(width, Math.Max(x0 + 1, (int)((x + 1) * scale)));
                double sum = 0;
                for (int yy = y0; yy < y1; yy++)
                {
                    for (int xx = x0; xx < x1; xx++)
                    {
                        sum += gray[yy * width + xx];
                    }
                }

                result[y * w + x] = sum / ((y1 - y0) * (x1 - x0));
            }
        }

        return result;
    }

    /// <summary>Repeated 3x3 box blur (close to a Gaussian).</summary>
    private static double[] Blur(double[] source, int w, int h, int passes)
    {
        double[] a = source, b = new double[source.Length];
        for (int pass = 0; pass < passes * 2; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sum = 0;
                    int n = 0;
                    for (int d = -1; d <= 1; d++)
                    {
                        int xx = x + d;
                        if (xx >= 0 && xx < w)
                        {
                            sum += a[y * w + xx];
                            n++;
                        }
                    }

                    b[y * w + x] = sum / n;
                }
            }

            double[] c = new double[source.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sum = 0;
                    int n = 0;
                    for (int d = -1; d <= 1; d++)
                    {
                        int yy = y + d;
                        if (yy >= 0 && yy < h)
                        {
                            sum += b[yy * w + x];
                            n++;
                        }
                    }

                    c[y * w + x] = sum / n;
                }
            }

            a = c;
        }

        return a;
    }

    private static (double[] Gx, double[] Gy, double[] Magnitude) Gradient(double[] image, int w, int h)
    {
        double[] gx = new double[w * h], gy = new double[w * h], m = new double[w * h];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                gx[i] = image[i + 1] - image[i - 1];
                gy[i] = image[i + w] - image[i - w];
                m[i] = Math.Sqrt(gx[i] * gx[i] + gy[i] * gy[i]);
            }
        }

        return (gx, gy, m);
    }

    /// <summary>Circle vote along the gradient direction. With <paramref name="near"/> only edges inside
    /// that disc vote; radii are fractions of the short image side unless explicit radii are given.</summary>
    private static (double, double, double)? FindCircle(
        double[] gx, double[] gy, double[] magnitude, int w, int h,
        double minFraction, double maxFraction, double percentile,
        (double X, double Y, double R)? near, CancellationToken cancellationToken,
        double minRadius = 0, double maxRadius = 0)
    {
        double[] sorted = (double[])magnitude.Clone();
        Array.Sort(sorted);
        double threshold = sorted[(int)(sorted.Length * percentile / 100)];
        if (threshold <= 0)
        {
            return null;
        }

        List<(int X, int Y, double Ux, double Uy)> edges = [];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (magnitude[i] <= threshold)
                {
                    continue;
                }

                if (near is { } n && (x - n.X) * (x - n.X) + (y - n.Y) * (y - n.Y) > n.R * n.R)
                {
                    continue;
                }

                edges.Add((x, y, gx[i] / magnitude[i], gy[i] / magnitude[i]));
            }
        }

        if (edges.Count < 50)
        {
            return null;
        }

        double shortSide = Math.Min(w, h);
        double low = minRadius > 0 ? minRadius : shortSide * minFraction;
        double high = maxRadius > 0 ? maxRadius : shortSide * maxFraction;
        double step = Math.Max(1, (high - low) / 70);
        (double Score, double X, double Y, double R) best = (0, 0, 0, 0);
        int[] accumulator = new int[w * h];
        for (double radius = low; radius < high; radius += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Array.Clear(accumulator);
            foreach (var e in edges)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int cx = (int)Math.Round(e.X + sign * radius * e.Ux), cy = (int)Math.Round(e.Y + sign * radius * e.Uy);
                    if (cx >= 0 && cx < w && cy >= 0 && cy < h)
                    {
                        accumulator[cy * w + cx]++;
                    }
                }
            }

            // Smooth over a small window so a slightly elliptical rim still piles up on one cell.
            int window = Math.Max(2, (int)(radius * 0.04));
            double[] integral = new double[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                double row = 0;
                for (int x = 0; x < w; x++)
                {
                    row += accumulator[y * w + x];
                    integral[(y + 1) * (w + 1) + x + 1] = integral[y * (w + 1) + x + 1] + row;
                }
            }

            for (int y = 0; y < h; y += 1)
            {
                int y0 = Math.Max(0, y - window), y1 = Math.Min(h, y + window + 1);
                for (int x = 0; x < w; x += 1)
                {
                    int x0 = Math.Max(0, x - window), x1 = Math.Min(w, x + window + 1);
                    double sum = integral[y1 * (w + 1) + x1] - integral[y0 * (w + 1) + x1]
                        - integral[y1 * (w + 1) + x0] + integral[y0 * (w + 1) + x0];
                    if (sum > best.Score)
                    {
                        best = (sum, x, y, radius);
                    }
                }
            }
        }

        return best.Score > 0 ? (best.X, best.Y, best.R) : null;
    }

    /// <summary>Strongest radial edge per direction, linked around the circle with a smoothness penalty.</summary>
    private static List<(double X, double Y)> FollowRing(
        double[] image, int w, int h, double cx, double cy, double r0, double r1, int angles, CancellationToken cancellationToken)
    {
        int radii = Math.Max(8, (int)(r1 - r0));
        double[,] edge = new double[angles, radii];
        for (int a = 0; a < angles; a++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double angle = 2 * Math.PI * a / angles, cos = Math.Cos(angle), sin = Math.Sin(angle);
            double[] values = new double[radii + 1];
            for (int j = 0; j <= radii; j++)
            {
                double r = r0 + (r1 - r0) * j / radii;
                values[j] = Sample(image, w, h, cx + r * cos, cy + r * sin);
            }

            double total = 0;
            for (int j = 0; j < radii; j++)
            {
                double d = j == 0 ? values[1] - values[0] : (values[j + 1] - values[Math.Max(0, j - 1)]) / 2;
                edge[a, j] = Math.Abs(d);
                total += edge[a, j];
            }

            double norm = total > 0 ? radii / total : 0;
            for (int j = 0; j < radii; j++)
            {
                edge[a, j] *= norm;
            }
        }

        const int reach = 3;
        const double penalty = 2.0;
        int[] path = Link(edge, angles, radii, reach, penalty);
        int split = angles / 2;
        double[,] rolled = new double[angles, radii];
        for (int a = 0; a < angles; a++)
        {
            for (int j = 0; j < radii; j++)
            {
                rolled[a, j] = edge[(a + split) % angles, j];
            }
        }

        int[] second = Link(rolled, angles, radii, reach, penalty);
        for (int a = 0; a < angles; a++)
        {
            path[(a + split) % angles] = second[a];
        }

        List<(double, double)> points = [];
        for (int a = 0; a < angles; a++)
        {
            double angle = 2 * Math.PI * a / angles;
            double r = r0 + (r1 - r0) * path[a] / radii;
            points.Add((cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }

        return points;
    }

    private static int[] Link(double[,] edge, int angles, int radii, int reach, double penalty)
    {
        double[,] score = new double[angles, radii];
        int[,] back = new int[angles, radii];
        for (int j = 0; j < radii; j++)
        {
            score[0, j] = edge[0, j];
        }

        for (int a = 1; a < angles; a++)
        {
            for (int j = 0; j < radii; j++)
            {
                double bestScore = double.NegativeInfinity;
                int bestK = j;
                for (int k = Math.Max(0, j - reach); k <= Math.Min(radii - 1, j + reach); k++)
                {
                    double candidate = score[a - 1, k] - penalty * Math.Abs(k - j);
                    if (candidate > bestScore)
                    {
                        bestScore = candidate;
                        bestK = k;
                    }
                }

                score[a, j] = bestScore + edge[a, j];
                back[a, j] = bestK;
            }
        }

        int end = 0;
        for (int j = 1; j < radii; j++)
        {
            if (score[angles - 1, j] > score[angles - 1, end])
            {
                end = j;
            }
        }

        int[] path = new int[angles];
        path[angles - 1] = end;
        for (int a = angles - 1; a > 0; a--)
        {
            path[a - 1] = back[a, path[a]];
        }

        return path;
    }

    private static double Sample(double[] image, int w, int h, double x, double y)
    {
        x = Math.Clamp(x, 0, w - 1.001);
        y = Math.Clamp(y, 0, h - 1.001);
        int x0 = (int)x, y0 = (int)y;
        double fx = x - x0, fy = y - y0;
        return image[y0 * w + x0] * (1 - fx) * (1 - fy) + image[y0 * w + x0 + 1] * fx * (1 - fy)
            + image[(y0 + 1) * w + x0] * (1 - fx) * fy + image[(y0 + 1) * w + x0 + 1] * fx * fy;
    }

    /// <summary>Ellipse from algebraic least squares, refitted after dropping points that disagree.</summary>
    private sealed class Ellipse
    {
        private readonly double[] _axes;
        private readonly double _cos, _sin;

        private Ellipse(double cx, double cy, double[] axes, double cos, double sin)
        {
            CenterX = cx;
            CenterY = cy;
            _axes = axes;
            _cos = cos;
            _sin = sin;
        }

        public double CenterX { get; }
        public double CenterY { get; }
        public double MeanRadius => (_axes[0] + _axes[1]) / 2;
        public bool IsFinite => _axes.All(a => double.IsFinite(a) && a > 0) && double.IsFinite(CenterX) && double.IsFinite(CenterY);

        public static Ellipse? Fit(IReadOnlyList<(double X, double Y)> points)
        {
            bool[] keep = new bool[points.Count];
            Array.Fill(keep, true);
            double mx = points.Average(p => p.X), my = points.Average(p => p.Y);
            double[]? conic = null;
            for (int round = 0; round < 4; round++)
            {
                conic = Solve(points, keep, mx, my);
                if (conic is null)
                {
                    return null;
                }

                double[] residual = new double[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    double x = points[i].X - mx, y = points[i].Y - my;
                    double value = conic[0] * x * x + conic[1] * x * y + conic[2] * y * y + conic[3] * x + conic[4] * y - 1;
                    double gradX = 2 * conic[0] * x + conic[1] * y + conic[3], gradY = conic[1] * x + 2 * conic[2] * y + conic[4];
                    residual[i] = Math.Abs(value) / Math.Max(1e-9, Math.Sqrt(gradX * gradX + gradY * gradY));
                }

                double[] sorted = residual.OrderBy(v => v).ToArray();
                double limit = Math.Max(2, 2.5 * sorted[sorted.Length / 2]);
                int kept = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    keep[i] = residual[i] <= limit;
                    kept += keep[i] ? 1 : 0;
                }

                if (kept < 8)
                {
                    break;
                }
            }

            if (conic is null)
            {
                return null;
            }

            // Centre: gradient of the conic vanishes. Then x'Mx = k with M = [[A, B/2],[B/2, C]].
            double a = conic[0], b = conic[1] / 2, c = conic[2], d = conic[3], e = conic[4];
            double det = 4 * (a * c - b * b);
            if (Math.Abs(det) < 1e-18 || a * c - b * b <= 0)
            {
                return null;
            }

            double cx = (-c * d + b * e) / (2 * (a * c - b * b));
            double cy = (-a * e + b * d) / (2 * (a * c - b * b));
            double k = 1 + a * cx * cx + 2 * b * cx * cy + c * cy * cy;
            double na = a / k, nb = b / k, nc = c / k;
            double theta = 0.5 * Math.Atan2(2 * nb, na - nc);
            double cos = Math.Cos(theta), sin = Math.Sin(theta);
            double l1 = na * cos * cos + 2 * nb * cos * sin + nc * sin * sin;
            double l2 = na * sin * sin - 2 * nb * cos * sin + nc * cos * cos;
            if (l1 <= 0 || l2 <= 0)
            {
                return null;
            }

            return new Ellipse(mx + cx, my + cy, [1 / Math.Sqrt(l1), 1 / Math.Sqrt(l2)], cos, sin);
        }

        /// <summary>Offset from the centre of the point at parameter t on this ellipse's shape,
        /// scaled by <paramref name="factor"/> (1 = the ellipse itself).</summary>
        public (double X, double Y) Local(double t, double factor)
        {
            double u = _axes[0] * Math.Cos(t) * factor, v = _axes[1] * Math.Sin(t) * factor;
            return (u * _cos - v * _sin, u * _sin + v * _cos);
        }

        /// <summary>Points on this ellipse's shape, scaled and moved; angle 0 is the top, clockwise.</summary>
        public CalibrationPoint[] Points(int count, double shapeScale, double cx, double cy, double toPhoto)
        {
            CalibrationPoint[] result = new CalibrationPoint[count];
            for (int i = 0; i < count; i++)
            {
                double t = -Math.PI / 2 + 2 * Math.PI * i / count;
                double u = _axes[0] * Math.Cos(t) * shapeScale, v = _axes[1] * Math.Sin(t) * shapeScale;
                result[i] = new CalibrationPoint(
                    (cx + u * _cos - v * _sin) * toPhoto,
                    (cy + u * _sin + v * _cos) * toPhoto);
            }

            return result;
        }

        private static double[]? Solve(IReadOnlyList<(double X, double Y)> points, bool[] keep, double mx, double my)
        {
            double[,] m = new double[5, 6];
            for (int i = 0; i < points.Count; i++)
            {
                if (!keep[i])
                {
                    continue;
                }

                double x = points[i].X - mx, y = points[i].Y - my;
                double[] row = [x * x, x * y, y * y, x, y];
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        m[r, c] += row[r] * row[c];
                    }

                    m[r, 5] += row[r];
                }
            }

            for (int col = 0; col < 5; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 5; r++)
                {
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col]))
                    {
                        pivot = r;
                    }
                }

                if (Math.Abs(m[pivot, col]) < 1e-12)
                {
                    return null;
                }

                if (pivot != col)
                {
                    for (int c = 0; c < 6; c++)
                    {
                        (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                    }
                }

                for (int r = 0; r < 5; r++)
                {
                    if (r == col)
                    {
                        continue;
                    }

                    double factor = m[r, col] / m[col, col];
                    for (int c = col; c < 6; c++)
                    {
                        m[r, c] -= factor * m[col, c];
                    }
                }
            }

            return [m[0, 5] / m[0, 0], m[1, 5] / m[1, 1], m[2, 5] / m[2, 2], m[3, 5] / m[3, 3], m[4, 5] / m[4, 4]];
        }
    }
}
