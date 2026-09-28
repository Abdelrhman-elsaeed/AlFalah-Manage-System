using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/recognitions")]
public sealed class RecognitionsController : StudentAffairsControllerBase
{
    public RecognitionsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecognitionRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.RecognitionCreate)) return PermissionDenied();
        var response = await Mediator.Send(new CreateRecognitionCommand(request), cancellationToken);
        return FromResponse(response, StatusCodes.Status201Created);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RecognitionListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.RecognitionView)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetRecognitionsQuery(query), cancellationToken));
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics([FromQuery] RecognitionListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.RecognitionViewStatistics)) return PermissionDenied();
        return FeatureNotImplemented("W6 Recognition Review");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.RecognitionView)) return PermissionDenied();
        return FromResponse(await Mediator.Send(new GetRecognitionByIdQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/correct")]
    public async Task<IActionResult> Correct(int id, [FromBody] CorrectRecognitionRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.RecognitionManage)) return PermissionDenied();
        return FeatureNotImplemented("W6 Recognition Review");
    }
}
