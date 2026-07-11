using AlFalah.Application.DTOs.Account;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

public interface IAccountService
{
    Task<ApiResponse<SignatureDto>> GetSignatureAsync(string userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateSignatureAsync(string userId, SignatureDto dto, CancellationToken cancellationToken = default);
}
