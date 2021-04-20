using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Nvidia.Clara.DicomAdapter.Database.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaraApplicationEntities",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false),
                    AeTitle = table.Column<string>(nullable: false),
                    OverwriteSameInstance = table.Column<bool>(nullable: false, defaultValue: false),
                    IgnoredSopClasses = table.Column<string>(nullable: true),
                    Processor = table.Column<string>(nullable: false),
                    ProcessorSettings = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaraApplicationEntities", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "DestinationApplicationEntities",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false),
                    AeTitle = table.Column<string>(nullable: false),
                    HostIp = table.Column<string>(nullable: false),
                    Port = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinationApplicationEntities", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "InferenceJobs",
                columns: table => new
                {
                    InferenceJobId = table.Column<Guid>(nullable: false),
                    JobId = table.Column<string>(nullable: true),
                    PayloadId = table.Column<string>(nullable: false),
                    JobPayloadsStoragePath = table.Column<string>(nullable: false),
                    TryCount = table.Column<int>(nullable: false),
                    State = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceJobs", x => x.InferenceJobId);
                });

            migrationBuilder.CreateTable(
                name: "InferenceRequests",
                columns: table => new
                {
                    InferenceRequestId = table.Column<Guid>(nullable: false, defaultValue: new Guid("14580e42-745d-4b76-a24c-24570d5f6568")),
                    TransactionId = table.Column<string>(nullable: false),
                    Priority = table.Column<byte>(nullable: false),
                    InputMetadata = table.Column<string>(nullable: true),
                    InputResources = table.Column<string>(nullable: true),
                    OutputResources = table.Column<string>(nullable: true),
                    JobId = table.Column<string>(nullable: false),
                    PayloadId = table.Column<string>(nullable: false),
                    State = table.Column<int>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    StoragePath = table.Column<string>(nullable: false),
                    TryCount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceRequests", x => x.InferenceRequestId);
                });

            migrationBuilder.CreateTable(
                name: "SourceApplicationEntities",
                columns: table => new
                {
                    AeTitle = table.Column<string>(nullable: false),
                    HostIp = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceApplicationEntities", x => x.AeTitle);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaraApplicationEntities");

            migrationBuilder.DropTable(
                name: "DestinationApplicationEntities");

            migrationBuilder.DropTable(
                name: "InferenceJobs");

            migrationBuilder.DropTable(
                name: "InferenceRequests");

            migrationBuilder.DropTable(
                name: "SourceApplicationEntities");
        }
    }
}
