using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Leave;

namespace TAMS.Application.Leave;

// --- Create leave type ---
public sealed record CreateLeaveTypeCommand(string Code, string Name) : IRequest<LeaveTypeDto>;

public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateLeaveTypeHandler : IRequestHandler<CreateLeaveTypeCommand, LeaveTypeDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLeaveTypeHandler(ILeaveRepository leave, IUnitOfWork unitOfWork)
    {
        _leave = leave;
        _unitOfWork = unitOfWork;
    }

    public async Task<LeaveTypeDto> Handle(CreateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        if (await _leave.TypeCodeExistsAsync(request.Code, cancellationToken))
        {
            throw new ConflictException($"A leave type with code '{request.Code}' already exists.");
        }

        var type = new LeaveType(request.Code, request.Name);
        await _leave.AddTypeAsync(type, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return LeaveTypeDto.FromEntity(type);
    }
}

// --- List leave types ---
public sealed record GetLeaveTypesQuery : IRequest<IReadOnlyList<LeaveTypeDto>>;

public sealed class GetLeaveTypesHandler : IRequestHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>
{
    private readonly ILeaveRepository _leave;
    public GetLeaveTypesHandler(ILeaveRepository leave) => _leave = leave;

    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(GetLeaveTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _leave.GetTypesAsync(cancellationToken);
        return types.Select(LeaveTypeDto.FromEntity).ToList();
    }
}

// --- Set/upsert an entitlement (fixed annual model) ---
public sealed record SetLeaveBalanceCommand(long EmployeeId, long LeaveTypeId, short Year, decimal EntitledDays)
    : IRequest<LeaveBalanceDto>;

public sealed class SetLeaveBalanceValidator : AbstractValidator<SetLeaveBalanceCommand>
{
    public SetLeaveBalanceValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.Year).GreaterThan((short)2000);
        RuleFor(x => x.EntitledDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetLeaveBalanceHandler : IRequestHandler<SetLeaveBalanceCommand, LeaveBalanceDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IUnitOfWork _unitOfWork;

    public SetLeaveBalanceHandler(ILeaveRepository leave, IUnitOfWork unitOfWork)
    {
        _leave = leave;
        _unitOfWork = unitOfWork;
    }

    public async Task<LeaveBalanceDto> Handle(SetLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        if (await _leave.GetTypeByIdAsync(request.LeaveTypeId, cancellationToken) is null)
        {
            throw new BusinessRuleException($"Leave type '{request.LeaveTypeId}' does not exist.");
        }

        var balance = await _leave.GetBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.Year, cancellationToken);
        if (balance is null)
        {
            balance = new LeaveBalance(request.EmployeeId, request.LeaveTypeId, request.Year, request.EntitledDays);
            await _leave.AddBalanceAsync(balance, cancellationToken);
        }
        else
        {
            balance.SetEntitlement(request.EntitledDays);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return LeaveBalanceDto.FromEntity(balance);
    }
}

// --- Get an employee's balances for a year ---
public sealed record GetLeaveBalancesQuery(long EmployeeId, short Year) : IRequest<IReadOnlyList<LeaveBalanceDto>>;

public sealed class GetLeaveBalancesHandler : IRequestHandler<GetLeaveBalancesQuery, IReadOnlyList<LeaveBalanceDto>>
{
    private readonly ILeaveRepository _leave;
    public GetLeaveBalancesHandler(ILeaveRepository leave) => _leave = leave;

    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetLeaveBalancesQuery request, CancellationToken cancellationToken)
    {
        var balances = await _leave.GetBalancesForEmployeeAsync(request.EmployeeId, request.Year, cancellationToken);
        return balances.Select(LeaveBalanceDto.FromEntity).ToList();
    }
}
