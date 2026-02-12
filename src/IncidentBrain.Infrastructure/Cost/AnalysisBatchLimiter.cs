using Microsoft.Extensions.Options;

namespace IncidentBrain.Infrastructure.Cost;

public class AnalysisBatchLimiter
{
    private readonly CostMonitorOptions _options;

    public AnalysisBatchLimiter(IOptions<CostMonitorOptions>? options = null)
    {
        _options = options?.Value ?? new CostMonitorOptions();
    }

    public IReadOnlyList<T> LimitBatch<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= _options.MaxLogsPerBatch) return items;
        return items.Take(_options.MaxLogsPerBatch).ToList();
    }

    public string TruncateSummary(string summary)
    {
        if (summary.Length <= _options.MaxSummaryLength) return summary;
        return summary[.._options.MaxSummaryLength] + "...";
    }
}
