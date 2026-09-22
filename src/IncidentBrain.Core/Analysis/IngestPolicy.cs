namespace IncidentBrain.Core.Analysis;

public static class IngestPolicy
{
    public const int MaxBatch = 50;

    public static readonly string[] DefaultServices = ["dj-api", "dj-worker"];

    public static bool IsAllowedService(string service, IReadOnlyList<string> allowed)
    {
        foreach (var name in allowed)
        {
            if (string.Equals(name, service, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
