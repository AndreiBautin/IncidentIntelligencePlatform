using System.Diagnostics;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure.AI;

namespace IncidentBrain.Api.Services;

public class SwitchableAIService : IAIService
{
    private readonly IAIProviderState _state;
    private readonly MockAIService _mock;
    private readonly OllamaAIService _ollama;
    private readonly ILogger<SwitchableAIService> _logger;

    public SwitchableAIService(IAIProviderState state, MockAIService mock, OllamaAIService ollama, ILogger<SwitchableAIService> logger)
    {
        _state = state;
        _mock = mock;
        _ollama = ollama;
        _logger = logger;
    }

    private IAIService Current => string.Equals(_state.Provider, "Ollama", StringComparison.OrdinalIgnoreCase) ? _ollama : _mock;

    public async Task<string> GenerateIncidentSummaryAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await Current.GenerateIncidentSummaryAsync(incident, contextPatterns, cancellationToken);
            _logger.LogInformation("AI GenerateIncidentSummary completed in {ElapsedMs}ms, Provider={Provider}", sw.ElapsedMilliseconds, _state.Provider);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI GenerateIncidentSummary failed after {ElapsedMs}ms, Provider={Provider}", sw.ElapsedMilliseconds, _state.Provider);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GenerateInvestigationStepsAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await Current.GenerateInvestigationStepsAsync(incident, contextPatterns, cancellationToken);
            _logger.LogInformation("AI GenerateInvestigationSteps completed in {ElapsedMs}ms, Provider={Provider}", sw.ElapsedMilliseconds, _state.Provider);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI GenerateInvestigationSteps failed after {ElapsedMs}ms, Provider={Provider}", sw.ElapsedMilliseconds, _state.Provider);
            throw;
        }
    }
}
