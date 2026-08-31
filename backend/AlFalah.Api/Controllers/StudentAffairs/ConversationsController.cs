using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/conversations")]
public sealed class ConversationsController : StudentAffairsControllerBase
{
    public ConversationsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ConversationListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetConversationsQuery(query), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingStartGuardianTeacher, PermissionNames.MessagingStartGuardianAdministration)) return PermissionDenied();
        var response = await Mediator.Send(new CreateConversationCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetConversationByIdQuery(id), cancellationToken));
    }

    [HttpGet("{id:int}/messages")]
    public async Task<IActionResult> Messages(int id, [FromQuery] ConversationMessageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetConversationMessagesQuery(id, query), cancellationToken));
    }

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingSend)) return PermissionDenied();
        return Ok(await Mediator.Send(new SendConversationMessageCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, [FromBody] MarkConversationReadRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new MarkConversationReadCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, [FromBody] CloseConversationRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.MessagingCloseThread)) return PermissionDenied();
        return Ok(await Mediator.Send(new CloseConversationCommand(id, request), cancellationToken));
    }
}
