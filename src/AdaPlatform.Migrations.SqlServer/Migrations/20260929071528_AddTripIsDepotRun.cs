using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaPlatform.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTripIsDepotRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDepotRun",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDepotRun",
                table: "Trips");
        }
    }
}
