using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace AlFalah.Application.Notifications;

public sealed class CenterService(ICenterRepository repository, ICurrentUserService user, IDataProtectionProvider protection)
{
    private readonly IDataProtector cursorProtector = protection.CreateProtector("AlFalah.PersonalCenter.Cursor.v1");
    private InboxScope? Scope => user.ActiveSchoolId is { } school && user.UserId is { Length: > 0 } id ? new(school, id) : null;

    public async Task<ApiResponse<CenterPage>> ListAsync(CenterQuery query, CancellationToken ct)
    {
        if (Scope is not { } scope || !await repository.HasAccessAsync(scope, ct)) return ApiResponse<CenterPage>.Fail("الإشعارات غير متاحة.");
        CenterPosition? before = null;
        if (query.Before is not null)
        {
            try { before = JsonSerializer.Deserialize<CenterPosition>(cursorProtector.Unprotect(query.Before.Length <= 4096 ? query.Before : "")); }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or FormatException) { }
            if (before is null || before.School != scope.SchoolId || before.User != scope.UserId
                || before.Category != query.Category || before.Unread != query.Unread) return ApiResponse<CenterPage>.Fail("أعد تحميل سجل الإشعارات.");
        }
        var limit = Math.Clamp(query.Limit, 1, 50);
        var rows = await repository.ListAsync(scope, query with { Limit = limit + 1 }, before, ct);
        var items = new List<CenterItem>();
        var states = await repository.TaskStatesAsync(rows.Take(limit).ToArray(), ct);
        foreach (var row in rows.Take(limit)) items.Add(new(row.Id, row.Category, row.Title, row.Preview, row.CreatedAtUtc,
            row.ReadAtUtc, states.GetValueOrDefault(row.Id, "info"), row.SourceType, row.SourceId));
        var last = rows.Take(limit).LastOrDefault();
        var next = rows.Count > limit && last is not null ? cursorProtector.Protect(JsonSerializer.Serialize(
            new CenterPosition(last.CreatedAtUtc, last.Id, scope.SchoolId, scope.UserId, query.Category, query.Unread))) : null;
        return ApiResponse<CenterPage>.Success(new(items, next));
    }

    public async Task<ApiResponse<CenterState>> StateAsync(CancellationToken ct) => Scope is { } scope && await repository.HasAccessAsync(scope, ct)
        ? ApiResponse<CenterState>.Success(new(await repository.CountAsync(scope, ct), true))
        : ApiResponse<CenterState>.Success(new(0, false));

    public async Task<ApiResponse<bool>> ReadAsync(Guid? id, CancellationToken ct) => Scope is { } scope && await repository.HasAccessAsync(scope, ct)
        && await repository.ReadAsync(scope, id, ct)
        ? ApiResponse<bool>.Success(true) : ApiResponse<bool>.Fail("الإشعارات غير متاحة.");

    public async Task<ApiResponse<CenterTarget>> TargetAsync(Guid id, CancellationToken ct)
    {
        if (Scope is not { } scope || !await repository.HasAccessAsync(scope, ct)
            || await repository.FindAsync(scope, id, ct) is not { } entry) return ApiResponse<CenterTarget>.Success(new(false));
        return ApiResponse<CenterTarget>.Success(await repository.TargetAsync(entry, ct));
    }
    public async Task<ApiResponse<CenterDetail>> DetailAsync(Guid id, CancellationToken ct)
    {
        if (Scope is not { } scope || !await repository.HasAccessAsync(scope, ct)
            || await repository.FindAsync(scope, id, ct) is not { } entry) return ApiResponse<CenterDetail>.Fail("هذا الإشعار غير متاح حاليًا.");
        return ApiResponse<CenterDetail>.Success(await repository.DetailAsync(entry, ct));
    }
}
