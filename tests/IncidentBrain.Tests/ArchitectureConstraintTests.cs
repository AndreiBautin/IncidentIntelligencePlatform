using Xunit;

namespace IncidentBrain.Tests;

public class ArchitectureConstraintTests
{
    [Fact]
    public void Core_csproj_has_no_package_references()
    {
        var xml = File.ReadAllText(FindCsproj("IncidentBrain.Core.csproj"));
        Assert.DoesNotContain("<PackageReference", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Core_does_not_reference_API_or_Infrastructure()
    {
        var xml = File.ReadAllText(FindCsproj("IncidentBrain.Core.csproj"));
        Assert.DoesNotContain("IncidentBrain.API", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IncidentBrain.Infrastructure", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Infrastructure_does_not_reference_API()
    {
        var xml = File.ReadAllText(FindCsproj("IncidentBrain.Infrastructure.csproj"));
        Assert.DoesNotContain("IncidentBrain.API", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IncidentBrain.Core", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void API_references_Core_and_Infrastructure_only_as_projects()
    {
        var xml = File.ReadAllText(FindCsproj("IncidentBrain.API.csproj"));
        Assert.Contains("IncidentBrain.Core", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IncidentBrain.Infrastructure", xml, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindCsproj(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var hits = dir.GetFiles(name, SearchOption.AllDirectories);
            if (hits.Length > 0)
                return hits[0].FullName;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(name);
    }
}
