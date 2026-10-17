using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace AlFalah.Infrastructure.Notifications;

/// <summary>Retry the whole transactional command with a fresh context, never a half-saved EF tracker.</summary>
public sealed class InboxCommandRetryBehavior<TRequest, TResponse>(IServiceScopeFactory scopes)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private static readonly AsyncLocal<bool> Retrying = new();
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (Retrying.Value || request is not (SendConversationMessageCommand or CreateConversationCommand
            or MarkConversationReadCommand or MarkNotificationReadCommand or MarkAllNotificationsReadCommand))
            return await next();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (attempt == 0) return await next();
                await using var scope = scopes.CreateAsyncScope();
                Retrying.Value = true;
                try { return (TResponse)(await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct))!; }
                finally { Retrying.Value = false; }
            }
            catch (Exception ex) when (attempt < 2 && IsDeadlock(ex))
            { await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1) + Random.Shared.Next(50)), ct); }
        }
    }
    private static bool IsDeadlock(Exception ex) => ex is SqlException { Number: 1205 }
        || ex.InnerException is { } inner && IsDeadlock(inner);
}
