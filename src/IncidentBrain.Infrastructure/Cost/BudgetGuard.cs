using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.Cost;

public class BudgetGuard
{
    private readonly ICostMonitor _costMonitor;

    public BudgetGuard(ICostMonitor costMonitor)
    {
        _costMonitor = costMonitor;
    }

    public bool AllowAnalysisRun(int batchSize, int estimatedSummaryLength)
    {
        return _costMonitor.IsWithinBudget();
    }

    public void RecordRun(int batchSize, int summaryLength)
    {
        _costMonitor.RecordAnalysisRun(batchSize, summaryLength);
    }
}
