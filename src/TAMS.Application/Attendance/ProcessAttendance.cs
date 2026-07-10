using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Scheduling;

namespace TAMS.Application.Attendance;

/// <summary>
/// Processes (or recomputes) an employee's attendance for a work date: resolves
/// the effective shift, gathers that day's punches, runs the pure calculator, and
/// upserts the record. Idempotent and safe to re-run. (FR-ATT-002/009.)
/// </summary>
public sealed record ProcessAttendanceCommand(long EmployeeId, DateOnly WorkDate) : IRequest<AttendanceRecordDto>;

public sealed class ProcessAttendanceHandler : IRequestHandler<ProcessAttendanceCommand, AttendanceRecordDto>
{
    private readonly IAttendanceRepository _attendance;
    private readonly ISchedulingRepository _scheduling;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AttendanceCalculator _calculator;
    private readonly ShiftResolver _shiftResolver;

    public ProcessAttendanceHandler(
        IAttendanceRepository attendance,
        ISchedulingRepository scheduling,
        IEmployeeRepository employees,
        IUnitOfWork unitOfWork,
        AttendanceCalculator calculator,
        ShiftResolver shiftResolver)
    {
        _attendance = attendance;
        _scheduling = scheduling;
        _employees = employees;
        _unitOfWork = unitOfWork;
        _calculator = calculator;
        _shiftResolver = shiftResolver;
    }

    public async Task<AttendanceRecordDto> Handle(ProcessAttendanceCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Employee", request.EmployeeId);

        // Resolve the shift in force on that date (effective-dated). (FR-SFT-003.)
        var assignments = await _scheduling.GetAssignmentsForEmployeeAsync(
            employee.Id, employee.PrimaryDepartmentId, cancellationToken);
        var shiftId = _shiftResolver.ResolveShiftId(request.WorkDate, employee.PrimaryDepartmentId, assignments);
        var shift = shiftId is null ? null : await _scheduling.GetShiftByIdAsync(shiftId.Value, cancellationToken);

        // Gather the day's punches and run the pure calculator.
        var punches = await _attendance.GetPunchesForDayAsync(employee.Id, request.WorkDate, cancellationToken);
        var inputs = punches
            .Select(p => new PunchInput(p.PunchedAtUtc, p.Direction))
            .ToList();

        // Leave integration arrives in Phase 4; for now no day is leave-covered.
        var result = _calculator.Calculate(request.WorkDate, shift, inputs, isLeaveCovered: false);

        // Upsert the record.
        var record = await _attendance.GetRecordAsync(employee.Id, request.WorkDate, cancellationToken);
        if (record is null)
        {
            record = new AttendanceRecord(employee.Id, request.WorkDate);
            record.ApplyCalculation(result, shiftId);
            await _attendance.AddRecordAsync(record, cancellationToken);
        }
        else
        {
            record.ApplyCalculation(result, shiftId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AttendanceRecordDto.FromEntity(record);
    }
}
