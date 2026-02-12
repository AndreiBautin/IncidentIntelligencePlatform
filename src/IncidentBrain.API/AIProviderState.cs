namespace IncidentBrain.Api;

/// <summary>
/// Runtime state for the current AI provider (Mock or Ollama).
/// Used by SwitchableAIService and by GET/PATCH /api/settings/ai.
/// </summary>
public interface IAIProviderState
{
    string Provider { get; set; }
}

public class AIProviderState : IAIProviderState
{
    public string Provider { get; set; } = "Mock";
}
