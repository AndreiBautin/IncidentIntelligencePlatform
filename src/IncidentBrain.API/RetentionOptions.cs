namespace IncidentBrain.Api;

public class RetentionOptions
{
    public const string Section = "Retention";
    public int MaxActiveIncidents { get; set; } = 10;
    public int MaxTotalIncidents { get; set; } = 100;
    public int MaxLogEntries { get; set; } = 5000;
    public int AutoResolveIdleMinutes { get; set; } = 15;
}
