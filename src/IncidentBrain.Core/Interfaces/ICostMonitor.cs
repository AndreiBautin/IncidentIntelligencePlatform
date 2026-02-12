namespace IncidentBrain.Core.Interfaces;

public interface ICostMonitor
{
    void RecordAnalysisRun(int batchSize, int summaryLength);
    bool IsWithinBudget();
    CostUsage? GetUsage();
}

public class CostUsage
{
    public int TotalBatchesProcessed { get; set; }
    public int TotalLogsProcessed { get; set; }
    public int TotalSummaryLength { get; set; }
}
