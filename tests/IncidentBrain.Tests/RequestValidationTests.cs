using IncidentBrain.Api;
using Xunit;

namespace IncidentBrain.Tests;

public class RequestValidationTests
{
    [Fact]
    public void Sanitize_null_or_whitespace_returns_empty()
    {
        Assert.Equal(string.Empty, RequestValidation.Sanitize(null, 10));
        Assert.Equal(string.Empty, RequestValidation.Sanitize("   ", 10));
    }

    [Fact]
    public void Sanitize_trims_and_keeps_short_values()
    {
        Assert.Equal("api-gateway", RequestValidation.Sanitize("  api-gateway  ", 200));
    }

    [Fact]
    public void Sanitize_truncates_to_max_length()
    {
        var input = new string('a', 50);
        var result = RequestValidation.Sanitize(input, 10);
        Assert.Equal(10, result.Length);
        Assert.Equal(new string('a', 10), result);
    }
}
