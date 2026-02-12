using Microsoft.Extensions.Options;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.Cost;

// TODO: Tenant billing
// TODO: Rate limiting middleware
// TODO: Token budget tracking
public class CostMonitor : ICostMonitor
{
    private readonly CostMonitorOptions _options;
    private int _totalBatchesProcessed;
    private int _totalLogsProcessed;
    private int _totalSummaryLength;

    public CostMonitor(IOptions<CostMonitorOptions>? options = null)
    {
        _options = options?.Value ?? new CostMonitorOptions();
    }

    public void RecordAnalysisRun(int batchSize, int summaryLength)
    {
        _totalBatchesProcessed++;
        _totalLogsProcessed += batchSize;
        _totalSummaryLength += summaryLength;
    }

    public bool IsWithinBudget()
    {
        if (_totalLogsProcessed > _options.MaxTotalLogsPerDay) return false;
        if (_totalSummaryLength > _options.MaxTotalSummaryLengthPerDay) return false;
        return true;
    }

    public CostUsage? GetUsage()
    {
        return new CostUsage
        {
            TotalBatchesProcessed = _totalBatchesProcessed,
            TotalLogsProcessed = _totalLogsProcessed,
            TotalSummaryLength = _totalSummaryLength
        };
    }
}

public class CostMonitorOptions
{
    public int MaxLogsPerBatch { get; set; } = 500;
    public int MaxSummaryLength { get; set; } = 500;
    public int ReanalysisCooldownSeconds { get; set; } = 60;
    public int MaxTotalLogsPerDay { get; set; } = 1_000_000;
    public int MaxTotalSummaryLengthPerDay { get; set; } = 100_000;
}
