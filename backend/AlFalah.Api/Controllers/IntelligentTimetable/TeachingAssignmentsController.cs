using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController, Authorize]
[Route("api/v1/intelligent-timetable/settings/profiles/{setupId:int}/assignments")]
public sealed class TeachingAssignmentsController(IMediator mediator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(int setupId, CancellationToken ct) => Respond(await mediator.Send(new GetTeachingAssignmentsQuery(setupId), ct));
    [HttpGet("teachers")] public async Task<IActionResult> Teachers(int setupId, [FromQuery] TeachingTeacherSearch search, CancellationToken ct) =>
        Respond(await mediator.Send(new SearchTeachingTeachersQuery(setupId, search), ct));
    [HttpPut] public async Task<IActionResult> Save(int setupId, SaveTeachingAssignmentsRequest request, CancellationToken ct) =>
        Respond(await mediator.Send(new SaveTeachingAssignmentsCommand(setupId, request), ct));
    [HttpDelete("{requirementId:int}")] public async Task<IActionResult> Unassign(int setupId, int requirementId, [FromQuery] int revision, CancellationToken ct) =>
        Respond(await mediator.Send(new UnassignTeachingSubjectCommand(setupId, requirementId, revision), ct));
    [HttpGet("statement/content")]
    public async Task<IActionResult> Statement(int setupId, [FromQuery] int revision, [FromQuery] string scope, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTeachingStatementQuery(setupId, revision, scope), ct);
        if (!result.IsSuccess) return Respond(result);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Assignment-Revision"] = result.Data!.Revision.ToString();
        Response.Headers["X-Assignment-Scope"] = result.Data.Scope;
        Response.Headers.ETag = "\"" + result.Data.Hash + "\"";
        return File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
    private IActionResult Respond<T>(ApiResponse<T> response) => response.IsSuccess ? Ok(response) : StatusCode(
        (response.Errors.FirstOrDefault() ?? response.Message) switch {
            TimetableSettingsHandlerSupport.PermissionDenied => 403,
            TimetableSettingsHandlerSupport.NotFound => 404,
            TimetableSettingsHandlerSupport.ConcurrencyConflict => 409,
            _ => 400
        }, response);
}
