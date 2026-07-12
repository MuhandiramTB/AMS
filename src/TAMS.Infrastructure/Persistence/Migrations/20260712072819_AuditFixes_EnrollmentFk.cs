using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditFixes_EnrollmentFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Enroll_EmployeeId",
                schema: "Workforce",
                table: "EmployeeDeviceEnrollment",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDeviceEnrollment_Employee_EmployeeId",
                schema: "Workforce",
                table: "EmployeeDeviceEnrollment",
                column: "EmployeeId",
                principalSchema: "Workforce",
                principalTable: "Employee",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDeviceEnrollment_Employee_EmployeeId",
                schema: "Workforce",
                table: "EmployeeDeviceEnrollment");

            migrationBuilder.DropIndex(
                name: "IX_Enroll_EmployeeId",
                schema: "Workforce",
                table: "EmployeeDeviceEnrollment");
        }
    }
}
