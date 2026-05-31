using System.Globalization;
using System.Text;

namespace Client.Domain;

/// <summary>Shared SVG chart math — smooth (Catmull-Rom → cubic bezier) paths.</summary>
public static class ChartMath
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string N(double v) => v.ToString("0.##", Inv);

    /// <summary>Smooth line path through points using Catmull-Rom converted to cubic beziers.</summary>
    public static string SmoothPath(IReadOnlyList<(double X, double Y)> pts)
    {
        if (pts.Count < 2) return "";
        var sb = new StringBuilder();
        sb.Append("M ").Append(N(pts[0].X)).Append(' ').Append(N(pts[0].Y));
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[i - 1 < 0 ? i : i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Count ? pts[i + 2] : p2;
            var c1x = p1.X + (p2.X - p0.X) / 6;
            var c1y = p1.Y + (p2.Y - p0.Y) / 6;
            var c2x = p2.X - (p3.X - p1.X) / 6;
            var c2y = p2.Y - (p3.Y - p1.Y) / 6;
            sb.Append(" C ").Append(N(c1x)).Append(' ').Append(N(c1y)).Append(", ")
              .Append(N(c2x)).Append(' ').Append(N(c2y)).Append(", ")
              .Append(N(p2.X)).Append(' ').Append(N(p2.Y));
        }
        return sb.ToString();
    }
}
