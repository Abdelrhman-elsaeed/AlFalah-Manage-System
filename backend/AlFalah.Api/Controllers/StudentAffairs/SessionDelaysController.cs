using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/session-delays")]
public sealed class SessionDelaysController : StudentAffairsControllerBase
{
    public SessionDelaysController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionDelayRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SessionDelayCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateSessionDelayCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SessionDelayListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SessionDelayView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSessionDelaysQuery(query), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SessionDelayView)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetSessionDelayByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/correct")]
    public async Task<IActionResult> Correct(int id, [FromBody] CorrectSessionDelayRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.SessionDelayCorrect)) return PermissionDenied();
        return Ok(await Mediator.Send(new CorrectSessionDelayCommand(id, request), cancellationToken));
    }
}
