using System.Net.Http.Json;
using System.Text.Json;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IncidentBrain.Infrastructure.AI;

public class OllamaAIService : IAIService
{
    public const string HttpClientName = "Ollama";
    private const string OllamaFallbackPrefix = "[Ollama unavailable — using fallback] ";
    private static readonly HashSet<string> PullTriggeredForModels = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaAIService> _logger;

    public OllamaAIService(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options, ILogger<OllamaAIService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateIncidentSummaryAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var snippet = incident.TopErrorPattern.Length > 200
            ? incident.TopErrorPattern[..200] + "..."
            : incident.TopErrorPattern;
        var prompt = $"You are an incident analyst. Summarize this incident in 1-2 sentences: Service: {incident.AffectedService}. Error pattern: {snippet}. Error count: {incident.ErrorCount}. Severity: {incident.Severity}. Reply with only the summary, no preamble.";
        var response = await CallOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (response is not null)
            return response.Trim();
        return OllamaFallbackPrefix + FallbackSummary(incident);
    }

    public async Task<IReadOnlyList<string>> GenerateInvestigationStepsAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var snippet = incident.TopErrorPattern.Length > 200
            ? incident.TopErrorPattern[..200] + "..."
            : incident.TopErrorPattern;
        var prompt = $"You are an incident analyst. List 3-5 short investigation steps for this incident. Service: {incident.AffectedService}. Error: {snippet}. Reply with a short numbered list, one step per line.";
        var response = await CallOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (response is not null)
        {
            var steps = ParseStepsResponse(response);
            if (steps.Count > 0)
                return steps;
        }
        return FallbackStepsWithPrefix(incident);
    }

    private async Task<string?> CallOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var body = new { model = _options.Model, prompt, stream = false };
            var resp = await client.PostAsJsonAsync("/api/generate", body, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            if (json.TryGetProperty("response", out var responseProp))
                return responseProp.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama call failed (model: {Model}, baseUrl: {BaseUrl}), using fallback. Ensure Ollama is running and the model is pulled.", _options.Model, _options.BaseUrl);
            if (_options.AutoPullIfMissing && !PullTriggeredForModels.Contains(_options.Model))
            {
                lock (PullTriggeredForModels)
                {
                    if (PullTriggeredForModels.Add(_options.Model))
                        _ = TriggerPullInBackgroundAsync();
                }
            }
        }
        return null;
    }

    private async Task TriggerPullInBackgroundAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            await client.PostAsJsonAsync("/api/pull", new { model = _options.Model, stream = false }, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Ignore; user can pull manually if needed
        }
    }

    private static List<string> ParseStepsResponse(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var steps = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            // Strip leading number/bullet (e.g. "1. " or "- ")
            var start = 0;
            while (start < trimmed.Length && (char.IsDigit(trimmed[start]) || trimmed[start] == '.' || trimmed[start] == '-' || trimmed[start] == ' '))
                start++;
            trimmed = trimmed[start..].Trim();
            if (trimmed.Length > 0)
                steps.Add(trimmed);
            if (steps.Count >= 10) break;
        }
        return steps;
    }

    private static string FallbackSummary(Incident incident)
    {
        return $"Elevated errors in {incident.AffectedService}: {incident.TopErrorPattern}. " +
               $"Error count: {incident.ErrorCount}. Severity: {incident.Severity}. Status: {incident.Status}.";
    }

    private static IReadOnlyList<string> FallbackSteps(Incident incident)
    {
        var patternPreview = incident.TopErrorPattern.Length > 50 ? incident.TopErrorPattern[..50] + "..." : incident.TopErrorPattern;
        return new List<string>
        {
            "Check recent deployments for " + incident.AffectedService,
            "Inspect error rate and latency for " + incident.AffectedService,
            "Review logs matching pattern: " + patternPreview,
            "Verify dependencies and downstream services",
            "Consider rollback if deployment-related"
        };
    }

    private static IReadOnlyList<string> FallbackStepsWithPrefix(Incident incident)
    {
        var steps = new List<string> { "Ollama was unavailable; steps below are fallback." };
        steps.AddRange(FallbackSteps(incident));
        return steps;
    }
}
