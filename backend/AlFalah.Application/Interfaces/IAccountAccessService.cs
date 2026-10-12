namespace AlFalah.Application.Interfaces;

public sealed record SignatureRequirementDto(bool Required, bool HasSignature);
public sealed record PasswordRevealDto(bool Available, string? Password);
public sealed record UserCredentialSnapshot(string UserId, string? Hash, string? ProtectedPassword, bool IsGlobalAdministrator);

public interface IAccountAccessService
{
    Task<SignatureRequirementDto> GetSignatureRequirementAsync(CancellationToken ct = default);
    Task<PasswordRevealDto> RevealPasswordAsync(string userId, CancellationToken ct = default);
}

public interface IAccountAccessRepository
{
    Task<bool> HasSignatureAsync(string userId, CancellationToken ct);
    Task<bool> IsActiveManagerAsync(string userId, int schoolId, CancellationToken ct);
    Task<UserCredentialSnapshot?> ReadCredentialAsync(string userId, int? schoolId, CancellationToken ct);
    Task RecordRevealAsync(string actorId, string targetId, int? schoolId, bool available, CancellationToken ct);
}
