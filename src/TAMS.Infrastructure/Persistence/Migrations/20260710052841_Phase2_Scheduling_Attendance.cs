using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_Scheduling_Attendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Attendance");

            migrationBuilder.EnsureSchema(
                name: "Scheduling");

            migrationBuilder.CreateTable(
                name: "AttendanceRecord",
                schema: "Attendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResolvedShiftId = table.Column<long>(type: "bigint", nullable: true),
                    FirstInUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkedMinutes = table.Column<int>(type: "int", nullable: true),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    EarlyLeaveMinutes = table.Column<int>(type: "int", nullable: false),
                    OvertimeMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PunchTransaction",
                schema: "Attendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    PunchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceType = table.Column<byte>(type: "tinyint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchTransaction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shift",
                schema: "Scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    GraceInMinutes = table.Column<int>(type: "int", nullable: false),
                    GraceOutMinutes = table.Column<int>(type: "int", nullable: false),
                    OvertimeThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shift", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceCorrection",
                schema: "Attendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<long>(type: "bigint", nullable: false),
                    CorrectedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrection_AttendanceRecord_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceException",
                schema: "Attendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<long>(type: "bigint", nullable: false),
                    ExceptionType = table.Column<byte>(type: "tinyint", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceException", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceException_AttendanceRecord_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalSchema: "Attendance",
                        principalTable: "AttendanceRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignment",
                schema: "Scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignment", x => x.Id);
                    table.CheckConstraint("CK_ShiftAssign_OneTarget", "(([EmployeeId] IS NOT NULL AND [DepartmentId] IS NULL) OR ([EmployeeId] IS NULL AND [DepartmentId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ShiftAssignment_Shift_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "Scheduling",
                        principalTable: "Shift",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrection_AttendanceRecordId",
                schema: "Attendance",
                table: "AttendanceCorrection",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceException_AttendanceRecordId",
                schema: "Attendance",
                table: "AttendanceException",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Exc_IsResolved",
                schema: "Attendance",
                table: "AttendanceException",
                column: "IsResolved",
                filter: "[IsResolved] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AttRec_WorkDate_Status",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_AttendanceRecord_Employee_WorkDate",
                schema: "Attendance",
                table: "AttendanceRecord",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Punch_DeviceId_PunchedAtUtc",
                schema: "Attendance",
                table: "PunchTransaction",
                columns: new[] { "DeviceId", "PunchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Punch_EmployeeId_PunchedAtUtc",
                schema: "Attendance",
                table: "PunchTransaction",
                columns: new[] { "EmployeeId", "PunchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_Punch_IdempotencyKey",
                schema: "Attendance",
                table: "PunchTransaction",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Shift_Code",
                schema: "Scheduling",
                table: "Shift",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssign_Emp_Effective",
                schema: "Scheduling",
                table: "ShiftAssignment",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignment_DepartmentId_EffectiveFrom",
                schema: "Scheduling",
                table: "ShiftAssignment",
                columns: new[] { "DepartmentId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignment_ShiftId",
                schema: "Scheduling",
                table: "ShiftAssignment",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrection",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "AttendanceException",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "PunchTransaction",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "ShiftAssignment",
                schema: "Scheduling");

            migrationBuilder.DropTable(
                name: "AttendanceRecord",
                schema: "Attendance");

            migrationBuilder.DropTable(
                name: "Shift",
                schema: "Scheduling");
        }
    }
}
