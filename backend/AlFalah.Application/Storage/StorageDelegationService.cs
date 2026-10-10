using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Storage;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class StorageDelegationService(IStorageRepository repository, IStorageAuthorizationService authorization,
    ICurrentUserService currentUser, TimeProvider time, IOptions<StorageOptions> options) : IStorageDelegationService
{
    public async Task<IReadOnlyList<StorageDelegationDto>> ListAsync(CancellationToken ct = default)
    {
        var schoolId = School();
        await authorization.RequireManagerAsync(schoolId, ct);
        return (await repository.GetDelegationsAsync(schoolId, ct)).Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<StorageDelegationCandidateDto>> CandidatesAsync(CancellationToken ct = default)
    {
        var schoolId = School();
        var manager = await authorization.RequireManagerAsync(schoolId, ct);
        return await repository.GetDelegationCandidatesAsync(schoolId, manager.UserId, ct);
    }

    public Task<StorageDelegationDto> GrantAsync(GrantStorageDelegationRequest request, CancellationToken ct = default)
    {
        var schoolId = School();
        Reason(request.Reason);
        if (string.IsNullOrWhiteSpace(request.GranteeUserId) || request.GranteeUserId.Length > 450)
            throw new ArgumentException("اختر مستخدمًا صالحًا.");
        return repository.InSerializableTransactionAsync(async () =>
        {
            var actor = await authorization.RequireManagerAsync(schoolId, ct);
            var now = time.GetUtcNow();
            if (request.StartsAt < now.AddMinutes(-5) || request.ExpiresAt.HasValue && request.ExpiresAt <= request.StartsAt)
                throw new ArgumentException("تاريخ التفويض غير صالح.");
            var grantee = await repository.GetActorScopeAsync(request.GranteeUserId, schoolId, ct);
            if (grantee is null || !grantee.IsMember || grantee.UserId == actor.UserId)
                throw new ArgumentException("الممنوح يجب أن يكون مستخدمًا نشطًا داخل المدرسة غير المدير.");
            if (await repository.HasOverlappingDelegationAsync(schoolId, request.GranteeUserId, request.StartsAt, request.ExpiresAt, ct))
                throw new StorageConflictException();
            var delegation = new StorageDelegation
            {
                SchoolId = schoolId, GranteeUserId = request.GranteeUserId,
                GrantedByManagerUserId = actor.UserId, StartsAt = request.StartsAt, ExpiresAt = request.ExpiresAt,
                Reason = request.Reason.Trim(), CreatedAtUtc = now, UpdatedAtUtc = now
            };
            await repository.AddDelegationAsync(delegation, ct);
            return Map(delegation);
        }, ct);
    }

    public Task<StorageDelegationDto> RevokeAsync(int id, RevokeStorageDelegationRequest request, CancellationToken ct = default)
    {
        var schoolId = School();
        Reason(request.Reason);
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw new ArgumentException("إصدار السجل غير صالح."); }
        if (version.Length != 8) throw new ArgumentException("إصدار السجل غير صالح.");
        return repository.InSerializableTransactionAsync(async () =>
        {
            var actor = await authorization.RequireManagerAsync(schoolId, ct);
            var delegation = await repository.GetDelegationAsync(schoolId, id, ct) ?? throw new KeyNotFoundException("التفويض غير موجود.");
            if (delegation.RevokedAt.HasValue) return Map(delegation);
            delegation.RevokedAt = time.GetUtcNow();
            delegation.RevokedByManagerUserId = actor.UserId;
            delegation.RevocationReason = request.Reason.Trim();
            await repository.RevokeDelegationAsync(delegation, version, ct);
            return Map(delegation);
        }, ct);
    }

    private int School()
    {
        if (!options.Value.AdministrationEnabled) throw new KeyNotFoundException("مساحة التخزين الجديدة غير مفعلة.");
        return currentUser.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("اختر المدرسة أولًا.");
    }

    private static void Reason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("السبب مطلوب، وبحد أقصى 1000 حرف.");
    }

    private static StorageDelegationDto Map(StorageDelegation d) => new(d.Id, d.GranteeUserId, d.GrantedByManagerUserId,
        d.StartsAt, d.ExpiresAt, d.RevokedAt, d.Reason, d.RevocationReason, d.RevokedByManagerUserId, Convert.ToBase64String(d.RowVersion));
}
