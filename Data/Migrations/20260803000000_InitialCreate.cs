using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insgram_Manager.Data.Migrations;

/// <summary>
/// Initial database schema for Insgram Manager.
/// Creates all tables required by the current application model.
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutomationJobs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                JobId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                ParametersJson = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                ResultMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutomationJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ImportRecords",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                SessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TotalRecords = table.Column<int>(type: "INTEGER", nullable: false),
                ImportedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                SkippedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                FailedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                AccountCount = table.Column<int>(type: "INTEGER", nullable: false),
                RelationshipCount = table.Column<int>(type: "INTEGER", nullable: false),
                ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "InstagramAccounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FullName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                Biography = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                ProfilePictureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                FollowerCount = table.Column<int>(type: "INTEGER", nullable: false),
                FollowingCount = table.Column<int>(type: "INTEGER", nullable: false),
                PostCount = table.Column<int>(type: "INTEGER", nullable: false),
                IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                SourceFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                ImportSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InstagramAccounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "InstagramRelationships",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FullName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                ProfileUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                RelationshipType = table.Column<int>(type: "INTEGER", nullable: false),
                SourceFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                ImportSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InstagramRelationships", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AutomationJobs_JobId",
            table: "AutomationJobs",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_AutomationJobs_Status",
            table: "AutomationJobs",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ImportRecords_SessionId",
            table: "ImportRecords",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_InstagramAccounts_Username",
            table: "InstagramAccounts",
            column: "Username");

        migrationBuilder.CreateIndex(
            name: "IX_InstagramRelationships_RelationshipType",
            table: "InstagramRelationships",
            column: "RelationshipType");

        migrationBuilder.CreateIndex(
            name: "IX_InstagramRelationships_Username",
            table: "InstagramRelationships",
            column: "Username");

        migrationBuilder.CreateIndex(
            name: "IX_Settings_Key",
            table: "Settings",
            column: "Key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AutomationJobs");
        migrationBuilder.DropTable(name: "ImportRecords");
        migrationBuilder.DropTable(name: "InstagramAccounts");
        migrationBuilder.DropTable(name: "InstagramRelationships");
        migrationBuilder.DropTable(name: "Settings");
    }
}
