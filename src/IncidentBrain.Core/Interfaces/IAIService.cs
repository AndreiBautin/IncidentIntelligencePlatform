using IncidentBrain.Core.Domain;

namespace IncidentBrain.Core.Interfaces;

public interface IAIService
{
    Task<string> GenerateIncidentSummaryAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GenerateInvestigationStepsAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default);
}
