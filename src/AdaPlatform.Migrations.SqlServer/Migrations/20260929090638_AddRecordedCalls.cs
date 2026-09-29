using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaPlatform.Migrations.SqlServer.Migrations
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
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    VehicleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    TripStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Line = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LineCourse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TripNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    Post = table.Column<short>(type: "smallint", nullable: false),
                    StopCode = table.Column<int>(type: "int", nullable: true),
                    StopName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlannedArrival = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedDeparture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualArrival = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualDeparture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Traction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
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
