using System.Collections.Frozen;
using System.Text.RegularExpressions;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.Analysis;

// TODO: Replace with embedding + vector DB
public partial class TfIdfLogAnalyzer : ILogAnalyzer
{
    private static readonly char[] SplitChars = { ' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '-', '_', '(', ')', '[', ']', '{', '}' };
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "is", "it", "as", "by", "with"
    };

    // A real error message is a fixed template plus a per-occurrence identifier (job id, request id,
    // trace id) - "job {guid} failed: ..." over and over is the common shape, not an edge case. Left
    // alone, a UUID's hyphens split it into four or five rare sub-tokens whose high IDF weight can
    // outweigh every word the messages actually share, so messages that are otherwise identical land
    // just under the similarity threshold and never cluster. Collapsing an id-shaped token to one
    // placeholder before tokenizing removes the noise while leaving genuinely different wording alone.
    // Applied to the raw message before splitting: SplitChars includes '-', so a UUID must be collapsed
    // to a placeholder while it is still one contiguous run, or the split ever reaches its own hyphens.
    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex UuidPattern();

    // Applied per-token after splitting, for an id that has no hyphens to trip the pattern above -
    // a hex or decimal run six digits or longer reads as an id rather than a word in any error message.
    [GeneratedRegex(@"^(?:[0-9a-fA-F]{6,}|[0-9]{6,})$")]
    private static partial Regex HexIdPattern();

    private const string IdPlaceholder = "id";

    public IReadOnlyList<IncidentCluster> ClusterMessages(IReadOnlyList<LogEntry> logs, double similarityThreshold = 0.5)
    {
        if (logs.Count == 0) return Array.Empty<IncidentCluster>();

        var messages = logs.Select(l => l.Message).Distinct().ToList();
        if (messages.Count == 0) return Array.Empty<IncidentCluster>();

        var vectors = ComputeTfIdfVectors(messages);
        var assigned = new bool[messages.Count];
        var clusters = new List<IncidentCluster>();

        for (var i = 0; i < messages.Count; i++)
        {
            if (assigned[i]) continue;
            var group = new List<int> { i };
            assigned[i] = true;
            for (var j = i + 1; j < messages.Count; j++)
            {
                if (assigned[j]) continue;
                var sim = CosineSimilarity(vectors[i], vectors[j]);
                if (sim >= similarityThreshold)
                {
                    group.Add(j);
                    assigned[j] = true;
                }
            }
            var repr = messages[group[0]];
            var count = 0;
            var first = DateTime.MaxValue;
            var last = DateTime.MinValue;
            foreach (var idx in group)
            {
                var log = logs.First(l => l.Message == messages[idx]);
                count += logs.Count(l => l.Message == messages[idx]);
                if (log.Timestamp < first) first = log.Timestamp;
                if (log.Timestamp > last) last = log.Timestamp;
            }
            var timeBucket = first.Ticks / TimeSpan.FromMinutes(5).Ticks;
            var stableId = $"cluster-{HashMessage(repr)}-{timeBucket}";
            clusters.Add(new IncidentCluster
            {
                Id = stableId,
                RepresentativeMessage = repr,
                MessageHashes = group.Select(idx => HashMessage(messages[idx])).ToList(),
                Count = count,
                FirstSeen = first,
                LastSeen = last,
                SimilarityThresholdUsed = similarityThreshold
            });
        }

        return clusters;
    }

    public string? ExtractTopPattern(IReadOnlyList<LogEntry> logs)
    {
        var errorLogs = logs.Where(l => string.Equals(l.Level, "error", StringComparison.OrdinalIgnoreCase)).ToList();
        if (errorLogs.Count == 0) return null;
        var clusters = ClusterMessages(errorLogs, 0.5);
        return clusters.OrderByDescending(c => c.Count).FirstOrDefault()?.RepresentativeMessage;
    }

    private static List<Dictionary<string, double>> ComputeTfIdfVectors(IReadOnlyList<string> documents)
    {
        var tokenized = documents.Select(d => Tokenize(d).ToList()).ToList();
        var allTerms = tokenized.SelectMany(t => t).Distinct().ToFrozenSet();
        var df = new Dictionary<string, int>();
        foreach (var term in allTerms)
        {
            df[term] = tokenized.Count(t => t.Contains(term));
        }
        var n = documents.Count;
        var vectors = new List<Dictionary<string, double>>();
        foreach (var docTokens in tokenized)
        {
            var tf = new Dictionary<string, double>();
            foreach (var t in docTokens)
            {
                if (!tf.ContainsKey(t)) tf[t] = 0;
                tf[t] += 1.0;
            }
            var norm = Math.Sqrt(tf.Values.Sum(x => x * x));
            var vec = new Dictionary<string, double>();
            foreach (var (term, freq) in tf)
            {
                var idf = Math.Log((n + 1.0) / (df.GetValueOrDefault(term, 0) + 1.0)) + 1.0;
                vec[term] = (freq / (norm > 0 ? norm : 1)) * idf;
            }
            vectors.Add(vec);
        }
        return vectors;
    }

    private static double CosineSimilarity(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        var allKeys = a.Keys.Union(b.Keys).ToHashSet();
        double dot = 0, na = 0, nb = 0;
        foreach (var k in allKeys)
        {
            var va = a.GetValueOrDefault(k, 0);
            var vb = b.GetValueOrDefault(k, 0);
            dot += va * vb;
            na += va * va;
            nb += vb * vb;
        }
        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        return denom > 0 ? dot / denom : 0;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var withoutUuids = UuidPattern().Replace(text, IdPlaceholder);
        return withoutUuids
            .Split(SplitChars, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Select(w => HexIdPattern().IsMatch(w) ? IdPlaceholder : w)
            .Where(w => w.Length > 1 && !StopWords.Contains(w));
    }

    private static string HashMessage(string message)
    {
        var h = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(h)[..16];
    }
}
