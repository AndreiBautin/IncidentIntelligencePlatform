using System.Text.Json;
using System.Text.RegularExpressions;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.Ingestion;

public class FileUploadSource : ILogIngestionSource
{
    public string Name => "FileUpload";

    public IReadOnlyList<LogEntry> Parse(ReadOnlySpan<char> content)
    {
        var text = content.ToString();
        if (TryParseJsonl(text, out var jsonl)) return jsonl;
        if (TryParseJsonArray(text, out var arr)) return arr;
        return ParsePlainText(text);
    }

    private static bool TryParseJsonl(string text, out List<LogEntry> entries)
    {
        entries = new List<LogEntry>();
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed[0] != '{') { entries.Clear(); return false; }
            try
            {
                var doc = JsonDocument.Parse(trimmed);
                var e = MapJsonToLogEntry(doc.RootElement, Guid.NewGuid().ToString());
                if (e != null) entries.Add(e);
            }
            catch
            {
                entries.Clear();
                return false;
            }
        }
        return entries.Count > 0;
    }

    private static bool TryParseJsonArray(string text, out List<LogEntry> entries)
    {
        entries = new List<LogEntry>();
        var trimmed = text.Trim();
        if (!trimmed.StartsWith('[')) return false;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var e = MapJsonToLogEntry(el, Guid.NewGuid().ToString());
                if (e != null) entries.Add(e);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static LogEntry? MapJsonToLogEntry(JsonElement el, string id)
    {
        var timestamp = el.TryGetProperty("timestamp", out var tp)
            ? ParseTimestamp(tp.GetString())
            : DateTime.UtcNow;
        var service = el.TryGetProperty("service", out var sp) ? sp.GetString() ?? "unknown" : "unknown";
        var level = el.TryGetProperty("level", out var lp) ? lp.GetString() ?? "info" : "info";
        var message = el.TryGetProperty("message", out var mp) ? mp.GetString() ?? "" : "";
        return new LogEntry
        {
            Id = id,
            Timestamp = timestamp,
            Service = service,
            Level = level,
            Message = message,
            RawPayload = el.GetRawText(),
            Source = LogSource.FileUpload
        };
    }

    private static DateTime ParseTimestamp(string? s)
    {
        if (string.IsNullOrEmpty(s)) return DateTime.UtcNow;
        if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) return dt;
        return DateTime.UtcNow;
    }

    private static readonly Regex PlainTextRegex = new(
        @"(\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}[Z\.\d]*)\s+(\S+)\s+(\w+)\s+(.+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static List<LogEntry> ParsePlainText(string text)
    {
        var entries = new List<LogEntry>();
        var matches = PlainTextRegex.Matches(text);
        foreach (Match m in matches)
        {
            if (!m.Success || m.Groups.Count < 5) continue;
            var ts = ParseTimestamp(m.Groups[1].Value);
            var service = m.Groups[2].Value;
            var level = m.Groups[3].Value;
            var message = m.Groups[4].Value;
            entries.Add(new LogEntry
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = ts,
                Service = service,
                Level = level,
                Message = message,
                Source = LogSource.FileUpload
            });
        }
        if (entries.Count == 0 && text.Trim().Length > 0)
        {
            entries.Add(new LogEntry
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Service = "unknown",
                Level = "info",
                Message = text.Trim(),
                Source = LogSource.FileUpload
            });
        }
        return entries;
    }
}
