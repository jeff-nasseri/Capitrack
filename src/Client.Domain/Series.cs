namespace Client.Domain;

/// <summary>
/// Deterministic smooth series for the small "trend" sparklines (ported from the V2
/// design's series()). These are indicative micro-trends toward the current value —
/// the real gain/loss/value figures shown alongside come from the API.
/// </summary>
public static class Series
{
    public static double[] Make(int seed, int n, double start, double end, double vol)
    {
        long s = seed % 2147483647; if (s <= 0) s += 2147483646;
        double Rng() { s = s * 16807 % 2147483647; return s / 2147483647.0; }

        var outp = new double[n];
        for (var i = 0; i < n; i++)
        {
            double t = i / (double)(n - 1);
            double smooth = start + (end - start) * (t * t * (3 - 2 * t));
            double wave = Math.Sin(t * Math.PI * 2.3 + seed) * (end - start) * 0.06;
            double noise = (Rng() - 0.5) * (end - start) * vol;
            outp[i] = Math.Max(smooth + wave + noise, start * 0.4);
        }
        outp[n - 1] = end;
        return outp;
    }

    public static int HashSeed(string sym)
    {
        int h = 0;
        foreach (var c in sym) h = (int)((h * 31L + c) % 99991);
        return h + 1;
    }

    public static double[] HoldingSeries(string sym, double price, double gp)
    {
        double start = price / (1 + gp / 100) * (gp >= 0 ? 0.92 : 1.08);
        return Make(HashSeed(sym), 40, start <= 0 ? price * 0.9 : start, price <= 0 ? 1 : price, 0.03);
    }

    public static double[] AccountSeries(int id, double cost, double value)
    {
        double start = cost > 0 ? cost : value * 0.8;
        return Make(id * 7 + 11, 40, start <= 0 ? 1 : start, value <= 0 ? 1 : value, 0.03);
    }
}
