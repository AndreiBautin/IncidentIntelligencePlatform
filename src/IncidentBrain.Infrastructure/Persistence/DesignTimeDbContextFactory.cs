using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IncidentBrain.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../IncidentBrain.API"))
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(config.GetConnectionString("Default") ?? "Data Source=incidentbrain.db")
            .Options;
        return new AppDbContext(options);
    }
}
