using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentBrain.Infrastructure.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Service = table.Column<string>(type: "TEXT", nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_DeploymentEvents", x => x.Id));

            migrationBuilder.CreateTable(
                name: "IncidentClusters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RepresentativeMessage = table.Column<string>(type: "TEXT", nullable: false),
                    MessageHashes = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SimilarityThresholdUsed = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_IncidentClusters", x => x.Id));

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Service = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    RawPayload = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    DeploymentId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_LogEntries", x => x.Id));

            migrationBuilder.CreateTable(
                name: "AnalysisMetadata",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentId = table.Column<string>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfigSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CooldownUntil = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_AnalysisMetadata", x => x.Id));

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedService = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TopErrorPattern = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedSteps = table.Column<string>(type: "TEXT", nullable: false),
                    ClusterId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AnalysisMetadataId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Incidents", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisMetadata_IncidentId",
                table: "AnalysisMetadata",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ClusterId",
                table: "Incidents",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Severity",
                table: "Incidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_StartTime",
                table: "Incidents",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status",
                table: "Incidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Service",
                table: "LogEntries",
                column: "Service");

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Timestamp",
                table: "LogEntries",
                column: "Timestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AnalysisMetadata");
            migrationBuilder.DropTable(name: "DeploymentEvents");
            migrationBuilder.DropTable(name: "IncidentClusters");
            migrationBuilder.DropTable(name: "Incidents");
            migrationBuilder.DropTable(name: "LogEntries");
        }
    }
}
