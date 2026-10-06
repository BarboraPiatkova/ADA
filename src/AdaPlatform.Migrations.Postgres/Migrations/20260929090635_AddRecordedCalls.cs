using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AdaPlatform.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordedCalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: true),
                    TripStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Line = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LineCourse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TripNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StationId = table.Column<int>(type: "integer", nullable: false),
                    Post = table.Column<short>(type: "smallint", nullable: false),
                    StopCode = table.Column<int>(type: "integer", nullable: true),
                    StopName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PlannedArrival = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlannedDeparture = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualArrival = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualDeparture = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Traction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordedCalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedCalls_Source_ExternalId",
                table: "RecordedCalls",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordedCalls_StationId_PlannedDeparture",
                table: "RecordedCalls",
                columns: new[] { "StationId", "PlannedDeparture" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedCalls_VehicleId_ActualArrival",
                table: "RecordedCalls",
                columns: new[] { "VehicleId", "ActualArrival" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordedCalls");
        }
    }
}
