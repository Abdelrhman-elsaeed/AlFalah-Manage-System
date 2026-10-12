using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Services;

public sealed class AccountAccessService(IAccountAccessRepository repository, ICurrentUserService currentUser,
    UserPasswordProtector protector) : IAccountAccessService
{
    public async Task<SignatureRequirementDto> GetSignatureRequirementAsync(CancellationToken ct = default)
    {
        var id = currentUser.UserId ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
        var hasSignature = await repository.HasSignatureAsync(id, ct).ConfigureAwait(false);
        return new(!currentUser.IsInRole(RoleNames.SchoolManager) && !hasSignature, hasSignature);
    }

    public async Task<PasswordRevealDto> RevealPasswordAsync(string userId, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.HasPermission(PermissionNames.UserEdit)
            || (!currentUser.IsGlobalAdmin() && !currentUser.IsInRole(RoleNames.SchoolManager)))
            throw new UnauthorizedSchoolAccessException("كشف كلمة المرور متاح للإدارة المخولة فقط.");
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول.");
        int? schoolId = null;
        if (!currentUser.IsGlobalAdmin())
        {
            schoolId = currentUser.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("يجب اختيار المدرسة.");
            if (!await repository.IsActiveManagerAsync(actorId, schoolId.Value, ct).ConfigureAwait(false))
                throw new UnauthorizedSchoolAccessException("لا يوجد إسناد إداري نشط لهذه المدرسة.");
        }
        var credential = await repository.ReadCredentialAsync(userId, schoolId, ct).ConfigureAwait(false)
            ?? throw new UnauthorizedSchoolAccessException("المستخدم غير متاح في نطاق إدارتك.");
        if (credential.IsGlobalAdministrator && !currentUser.IsInRole(RoleNames.SuperAdmin))
            throw new UnauthorizedSchoolAccessException("لا يمكنك كشف كلمة مرور حساب إدارة عامة.");
        var password = protector.Read(credential.UserId, credential.ProtectedPassword, credential.Hash);
        await repository.RecordRevealAsync(actorId, userId, schoolId, password is not null, ct).ConfigureAwait(false);
        return new(password is not null, password);
    }
}
