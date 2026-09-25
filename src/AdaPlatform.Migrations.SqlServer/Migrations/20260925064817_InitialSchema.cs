using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaPlatform.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stops",
                columns: table => new
                {
                    Code = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tariffs = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Altitude = table.Column<double>(type: "float", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    JtskX = table.Column<double>(type: "float", nullable: true),
                    JtskY = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stops", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Depot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Traction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SeatingCapacity = table.Column<int>(type: "int", nullable: true),
                    StandingCapacity = table.Column<int>(type: "int", nullable: true),
                    IsExcluded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patterns",
                columns: table => new
                {
                    Code = table.Column<int>(type: "int", nullable: false),
                    TargetCode = table.Column<int>(type: "int", nullable: false),
                    FirstStopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastStopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LengthKm = table.Column<double>(type: "float", nullable: true),
                    LineId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patterns", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Patterns_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountingDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DeviceNumber = table.Column<int>(type: "int", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountingDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountingDevices_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    MalformedLineCount = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceFiles_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatternStops",
                columns: table => new
                {
                    PatternCode = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    StopCode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatternStops", x => new { x.PatternCode, x.Sequence });
                    table.ForeignKey(
                        name: "FK_PatternStops_Patterns_PatternCode",
                        column: x => x.PatternCode,
                        principalTable: "Patterns",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatternStops_Stops_StopCode",
                        column: x => x.StopCode,
                        principalTable: "Stops",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFileId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DeviceNumber = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    BlockCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LineId = table.Column<int>(type: "int", nullable: true),
                    LastStopCode = table.Column<int>(type: "int", nullable: true),
                    DelaySeconds = table.Column<int>(type: "int", nullable: true),
                    StopCode = table.Column<int>(type: "int", nullable: true),
                    PatternCode = table.Column<int>(type: "int", nullable: true),
                    Boardings = table.Column<int>(type: "int", nullable: true),
                    Alightings = table.Column<int>(type: "int", nullable: true),
                    OnBoard = table.Column<int>(type: "int", nullable: true),
                    OnBoardChange = table.Column<int>(type: "int", nullable: true),
                    Alive = table.Column<bool>(type: "bit", nullable: true),
                    StatusRegister = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvalidDevices = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceEvents_SourceFiles_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "SourceFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    SecondVehicleId = table.Column<int>(type: "int", nullable: true),
                    PatternCode = table.Column<int>(type: "int", nullable: true),
                    BlockCode = table.Column<string>(type: "nvarchar(20)", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitialDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    Boardings = table.Column<int>(type: "int", nullable: false),
                    Alightings = table.Column<int>(type: "int", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceFileId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Blocks_BlockCode",
                        column: x => x.BlockCode,
                        principalTable: "Blocks",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Patterns_PatternCode",
                        column: x => x.PatternCode,
                        principalTable: "Patterns",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_SourceFiles_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "SourceFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Vehicles_SecondVehicleId",
                        column: x => x.SecondVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceFaults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DeviceNumber = table.Column<int>(type: "int", nullable: true),
                    TripId = table.Column<long>(type: "bigint", nullable: true),
                    From = table.Column<DateTime>(type: "datetime2", nullable: false),
                    To = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceFaults_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StopVisits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    StopCode = table.Column<int>(type: "int", nullable: false),
                    ArrivalTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepartureTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DelaySeconds = table.Column<int>(type: "int", nullable: false),
                    Boardings = table.Column<int>(type: "int", nullable: false),
                    Alightings = table.Column<int>(type: "int", nullable: false),
                    Occupancy = table.Column<int>(type: "int", nullable: false),
                    IsPassThrough = table.Column<bool>(type: "bit", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StopVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StopVisits_Stops_StopCode",
                        column: x => x.StopCode,
                        principalTable: "Stops",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StopVisits_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoorCounts",
                columns: table => new
                {
                    StopVisitId = table.Column<long>(type: "bigint", nullable: false),
                    CountingDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    Boardings = table.Column<int>(type: "int", nullable: false),
                    Alightings = table.Column<int>(type: "int", nullable: false),
                    IsFlaggedInvalid = table.Column<bool>(type: "bit", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoorCounts", x => new { x.StopVisitId, x.CountingDeviceId });
                    table.ForeignKey(
                        name: "FK_DoorCounts_CountingDevices_CountingDeviceId",
                        column: x => x.CountingDeviceId,
                        principalTable: "CountingDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoorCounts_StopVisits_StopVisitId",
                        column: x => x.StopVisitId,
                        principalTable: "StopVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountingDevices_VehicleId_DeviceNumber",
                table: "CountingDevices",
                columns: new[] { "VehicleId", "DeviceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_SourceFileId_LineNumber",
                table: "DeviceEvents",
                columns: new[] { "SourceFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_Type",
                table: "DeviceEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_VehicleId_DeviceNumber_Time",
                table: "DeviceEvents",
                columns: new[] { "VehicleId", "DeviceNumber", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_VehicleId_Time",
                table: "DeviceEvents",
                columns: new[] { "VehicleId", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFaults_TripId",
                table: "DeviceFaults",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFaults_VehicleId_DeviceNumber_From",
                table: "DeviceFaults",
                columns: new[] { "VehicleId", "DeviceNumber", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_DoorCounts_CountingDeviceId",
                table: "DoorCounts",
                column: "CountingDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Patterns_LineId",
                table: "Patterns",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_PatternStops_StopCode",
                table: "PatternStops",
                column: "StopCode");

            migrationBuilder.CreateIndex(
                name: "IX_SourceFiles_Sha256",
                table: "SourceFiles",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceFiles_VehicleId_ServiceDate",
                table: "SourceFiles",
                columns: new[] { "VehicleId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StopVisits_StopCode",
                table: "StopVisits",
                column: "StopCode");

            migrationBuilder.CreateIndex(
                name: "IX_StopVisits_TripId_Sequence",
                table: "StopVisits",
                columns: new[] { "TripId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_BlockCode",
                table: "Trips",
                column: "BlockCode");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_PatternCode_StartTime",
                table: "Trips",
                columns: new[] { "PatternCode", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_SecondVehicleId",
                table: "Trips",
                column: "SecondVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_SourceFileId",
                table: "Trips",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_StartTime",
                table: "Trips",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId_StartTime",
                table: "Trips",
                columns: new[] { "VehicleId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceEvents");

            migrationBuilder.DropTable(
                name: "DeviceFaults");

            migrationBuilder.DropTable(
                name: "DoorCounts");

            migrationBuilder.DropTable(
                name: "PatternStops");

            migrationBuilder.DropTable(
                name: "CountingDevices");

            migrationBuilder.DropTable(
                name: "StopVisits");

            migrationBuilder.DropTable(
                name: "Stops");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Patterns");

            migrationBuilder.DropTable(
                name: "SourceFiles");

            migrationBuilder.DropTable(
                name: "Lines");

            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
