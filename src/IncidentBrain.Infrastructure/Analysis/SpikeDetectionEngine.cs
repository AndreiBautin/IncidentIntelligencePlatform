using IncidentBrain.Core.Domain;

namespace IncidentBrain.Infrastructure.Analysis;

public class SpikeDetectionEngine
{
    private readonly SpikeDetectionOptions _options;
    private const int WindowMinutes = 5;

    public SpikeDetectionEngine(SpikeDetectionOptions? options = null)
    {
        _options = options ?? new SpikeDetectionOptions();
    }

    public IReadOnlyList<SpikeResult> DetectSpikes(IReadOnlyList<LogEntry> logs)
    {
        if (logs.Count == 0) return Array.Empty<SpikeResult>();
        var byService = logs.GroupBy(l => l.Service).ToList();
        var results = new List<SpikeResult>();
        foreach (var group in byService)
        {
            var buckets = group
                .GroupBy(l => TruncateToWindow(l.Timestamp))
                .OrderBy(g => g.Key)
                .Select(g => new { Bucket = g.Key, Count = g.Count() })
                .ToList();
            if (buckets.Count < 2) continue;
            var baseline = buckets.Take(Math.Max(1, buckets.Count - 6)).Average(b => b.Count);
            if (baseline <= 0) baseline = 1;
            foreach (var b in buckets.Skip(buckets.Count - 3))
            {
                var ratio = b.Count / baseline;
                if (ratio >= _options.BaselineMultiplier)
                {
                    var severity = RatioToSeverity(ratio);
                    results.Add(new SpikeResult
                    {
                        Service = group.Key,
                        BucketStart = b.Bucket,
                        CurrentCount = b.Count,
                        BaselineCount = (int)baseline,
                        Ratio = ratio,
                        Severity = severity
                    });
                }
            }
        }
        return results.DistinctBy(s => (s.Service, s.BucketStart)).ToList();
    }

    private Severity RatioToSeverity(double ratio)
    {
        if (ratio >= _options.P1Threshold) return Severity.P1;
        if (ratio >= _options.P2Threshold) return Severity.P2;
        return Severity.P3;
    }

    private static DateTime TruncateToWindow(DateTime dt)
    {
        var ticks = dt.Ticks - (dt.Ticks % (TimeSpan.TicksPerMinute * WindowMinutes));
        return new DateTime(ticks, dt.Kind);
    }
}

public class SpikeDetectionOptions
{
    public double BaselineMultiplier { get; set; } = 2.0;
    public double P1Threshold { get; set; } = 5.0;
    public double P2Threshold { get; set; } = 2.0;
    public double P3Threshold { get; set; } = 1.5;
}

public class SpikeResult
{
    public string Service { get; set; } = string.Empty;
    public DateTime BucketStart { get; set; }
    public int CurrentCount { get; set; }
    public int BaselineCount { get; set; }
    public double Ratio { get; set; }
    public Severity Severity { get; set; }
}
