using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TAMS.Api.Controllers;

/// <summary>
/// Base for API controllers. Controllers are thin: they bind/authorize, dispatch
/// to MediatR, and translate the result to HTTP — no business logic. (07 §4.1.)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
