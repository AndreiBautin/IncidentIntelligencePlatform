using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using IncidentBrain.Core.Domain;

#nullable disable

namespace IncidentBrain.Infrastructure.Migrations
{
    [DbContext(typeof(Persistence.AppDbContext))]
    public partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogEntry>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Timestamp);
                e.HasIndex(x => x.Service);
            });

            modelBuilder.Entity<Incident>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.Severity);
                e.HasIndex(x => x.ClusterId);
                e.HasIndex(x => x.StartTime);
            });

            modelBuilder.Entity<IncidentCluster>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<DeploymentEvent>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<AnalysisMetadata>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.IncidentId);
            });
        }
    }
}
