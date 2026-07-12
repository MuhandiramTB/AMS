using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Identity;

namespace TAMS.Application.Users;

// --- DTOs ---
public sealed record UserDto(long Id, string UserName, string Email, IReadOnlyList<string> Roles, bool IsActive, long? EmployeeId, string? LastLoginUtc)
{
    public static UserDto FromEntity(User u) => new(
        u.Id, u.UserName, u.Email,
        u.Roles.Select(r => r.Name).OrderBy(n => n).ToList(),
        u.IsActive, u.EmployeeId, u.LastLoginUtc?.ToString("O"));
}

public sealed record RoleDto(string Name, string? Description);

// --- List users (User.Manage) ---
public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _users;
    public GetUsersHandler(IUserRepository users) => _users = users;

    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _users.ListAsync(cancellationToken);
        return users.Select(UserDto.FromEntity).ToList();
    }
}

// --- List roles (for the create/edit form) ---
public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class GetRolesHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IUserRepository _users;
    public GetRolesHandler(IUserRepository users) => _users = users;

    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _users.GetAllRolesAsync(cancellationToken);
        return roles.Select(r => new RoleDto(r.Name, r.Description)).ToList();
    }
}

// --- Create user (User.Manage) ---
public sealed record CreateUserCommand(string UserName, string Email, string Password, IReadOnlyList<string> Roles, long? EmployeeId = null) : IRequest<UserDto>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role is required.");
    }
}

/// <summary>Only an Administrator may grant the Administrator role — this prevents a
/// User.Manage operator (who is not an admin) from self-escalating. (06 §5.)</summary>
internal static class RoleEscalationGuard
{
    public static void EnsureAllowed(IReadOnlyList<string> requestedRoles, ICurrentUser currentUser)
    {
        var grantsAdmin = requestedRoles.Contains(RoleNames.Administrator);
        if (grantsAdmin && !currentUser.HasPermission(Permissions.RoleManage))
        {
            throw new ForbiddenException("Only an administrator may assign the Administrator role.");
        }
    }
}

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _users;
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreateUserHandler(IUserRepository users, IEmployeeRepository employees, IPasswordHasher hasher, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _users = users;
        _employees = employees;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        RoleEscalationGuard.EnsureAllowed(request.Roles, _currentUser);
        if (await _users.UserNameExistsAsync(request.UserName, cancellationToken))
        {
            throw new ConflictException($"A user named '{request.UserName}' already exists.");
        }

        var roles = await _users.GetRolesByNameAsync(request.Roles, cancellationToken);
        if (roles.Count != request.Roles.Distinct().Count())
        {
            throw new BusinessRuleException("One or more roles do not exist.");
        }

        if (request.EmployeeId is { } empId)
        {
            if (!await _employees.ExistsAsync(empId, cancellationToken))
            {
                throw new BusinessRuleException($"Employee '{empId}' does not exist.");
            }
            if (await _users.EmployeeLinkExistsAsync(empId, null, cancellationToken))
            {
                throw new ConflictException($"Employee '{empId}' is already linked to another user.");
            }
        }

        var user = new User(request.UserName, request.Email, _hasher.Hash(request.Password), request.EmployeeId);
        user.SetRoles(roles);
        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }
}

// --- Update user (email + roles + employee link; optional password reset) ---
public sealed record UpdateUserCommand(long Id, string Email, IReadOnlyList<string> Roles, string? NewPassword, long? EmployeeId = null) : IRequest<UserDto>;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role is required.");
        RuleFor(x => x.NewPassword).MinimumLength(8).When(x => !string.IsNullOrEmpty(x.NewPassword))
            .WithMessage("Password must be at least 8 characters.");
    }
}

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _users;
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdateUserHandler(IUserRepository users, IEmployeeRepository employees, IPasswordHasher hasher, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _users = users;
        _employees = employees;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        RoleEscalationGuard.EnsureAllowed(request.Roles, _currentUser);

        var user = await _users.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        var roles = await _users.GetRolesByNameAsync(request.Roles, cancellationToken);
        if (roles.Count != request.Roles.Distinct().Count())
        {
            throw new BusinessRuleException("One or more roles do not exist.");
        }

        if (request.EmployeeId is { } empId)
        {
            if (!await _employees.ExistsAsync(empId, cancellationToken))
            {
                throw new BusinessRuleException($"Employee '{empId}' does not exist.");
            }
            if (await _users.EmployeeLinkExistsAsync(empId, request.Id, cancellationToken))
            {
                throw new ConflictException($"Employee '{empId}' is already linked to another user.");
            }
        }

        user.UpdateEmail(request.Email);
        user.SetRoles(roles);
        user.LinkToEmployee(request.EmployeeId);
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.SetPasswordHash(_hasher.Hash(request.NewPassword));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }
}

// --- Activate / deactivate user (User.Manage) ---
public sealed record SetUserActiveCommand(long Id, bool Active) : IRequest<UserDto>;

public sealed class SetUserActiveHandler : IRequestHandler<SetUserActiveCommand, UserDto>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetUserActiveHandler(IUserRepository users, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _users = users;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserDto> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        if (!request.Active)
        {
            // Guard 1: an admin cannot deactivate their own account.
            if (_currentUser.UserId == user.Id)
            {
                throw new BusinessRuleException("You cannot deactivate your own account.");
            }

            // Guard 2: never leave the system without an active administrator.
            var isAdmin = user.Roles.Any(r => r.Name == RoleNames.Administrator);
            if (isAdmin && await _users.CountActiveInRoleAsync(RoleNames.Administrator, cancellationToken) <= 1)
            {
                throw new BusinessRuleException("Cannot deactivate the last active administrator.");
            }

            user.Deactivate();
        }
        else
        {
            user.Reactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }
}
