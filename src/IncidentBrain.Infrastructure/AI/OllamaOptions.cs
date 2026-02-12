namespace IncidentBrain.Infrastructure.AI;

public class OllamaOptions
{
    public const string Section = "AI:Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2:3b";
    public int TimeoutSeconds { get; set; } = 60;
    /// <summary>When true, if the model is missing we trigger a background pull via Ollama API so you don't need to run 'ollama pull' manually.</summary>
    public bool AutoPullIfMissing { get; set; } = true;
}
