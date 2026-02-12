namespace IncidentBrain.Api;

public static class RequestValidation
{
    public const int MaxServiceLength = 200;
    public const int MaxLevelLength = 50;
    public const int MaxMessageLength = 50_000;
    public const int MaxSearchLength = 500;

    public static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
