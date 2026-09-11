using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize]
[Route("api/v1/intelligent-timetable/settings/profiles/{setupId:int}/subjects")]
public sealed class TimetableSubjectsController(IMediator mediator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(int setupId, CancellationToken ct) => Respond(await mediator.Send(new GetSubjectsQuery(setupId), ct));
    [HttpPost] public async Task<IActionResult> Create(int setupId, SaveSubjectRequest request, CancellationToken ct) => Respond(await mediator.Send(new CreateSubjectCommand(setupId, request), ct));
    [HttpPut("{subjectId:int}")] public async Task<IActionResult> Update(int setupId, int subjectId, SaveSubjectRequest request, CancellationToken ct) => Respond(await mediator.Send(new UpdateSubjectCommand(setupId, subjectId, request), ct));
    [HttpPost("rooms")] public async Task<IActionResult> CreateRoom(int setupId, CreateRoomRequest request, CancellationToken ct) => Respond(await mediator.Send(new CreateSubjectRoomCommand(setupId, request), ct));
    [HttpPost("allocations")] public async Task<IActionResult> Allocate(int setupId, AllocateSubjectRequest request, CancellationToken ct) => Respond(await mediator.Send(new AllocateSubjectToClassesCommand(setupId, request), ct));
    [HttpPut("requirements/{id:int}")] public async Task<IActionResult> UpdateRequirement(int setupId, int id, UpdateSubjectRequirementsRequest request, CancellationToken ct) => Respond(await mediator.Send(new UpdateSubjectRequirementsCommand(setupId, id, request), ct));
    [HttpDelete("requirements/{id:int}")] public async Task<IActionResult> Remove(int setupId, int id, [FromQuery] int revision, CancellationToken ct) => Respond(await mediator.Send(new RemoveSubjectRequirementCommand(setupId, id, revision), ct));
    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403,
            TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409,
            _ => 400
        }, response);
}
