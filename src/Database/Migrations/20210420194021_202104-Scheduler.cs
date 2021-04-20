using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Nvidia.Clara.DicomAdapter.Database.Migrations
{
    public partial class _202104Scheduler : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobName",
                table: "InferenceJobs",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdate",
                table: "InferenceJobs",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PipelineId",
                table: "InferenceJobs",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "InferenceJobs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "InferenceJobs",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobName",
                table: "InferenceJobs");

            migrationBuilder.DropColumn(
                name: "LastUpdate",
                table: "InferenceJobs");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "InferenceJobs");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "InferenceJobs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "InferenceJobs");
        }
    }
}
