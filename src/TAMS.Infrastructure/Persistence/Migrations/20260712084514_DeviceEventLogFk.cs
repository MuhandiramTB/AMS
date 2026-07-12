using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceEventLogFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_DeviceEventLog_Device_DeviceId",
                schema: "Devices",
                table: "DeviceEventLog",
                column: "DeviceId",
                principalSchema: "Devices",
                principalTable: "Device",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceEventLog_Device_DeviceId",
                schema: "Devices",
                table: "DeviceEventLog");
        }
    }
}
