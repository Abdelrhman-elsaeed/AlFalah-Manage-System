using AlFalah.Application.DTOs.Account;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ApiResponse<SignatureDto>> GetSignatureAsync(string userId, CancellationToken cancellationToken = default)
    {
        var signature = await _context.Set<UserSignature>()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        return ApiResponse<SignatureDto>.Success(new SignatureDto
        {
            SignatureDrawnData = signature?.SignatureDrawnData
        });
    }

    public async Task<ApiResponse<bool>> UpdateSignatureAsync(string userId, SignatureDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ApiResponse<bool>.Fail("User not found");

        if (!string.IsNullOrEmpty(dto.SignatureDrawnData))
        {
            // Validate size cap (e.g. 500KB max base64) to prevent DOS
            if (dto.SignatureDrawnData.Length > 500 * 1024)
                return ApiResponse<bool>.Fail("Signature image is too large.");

            // Validate PNG data URL
            if (!dto.SignatureDrawnData.StartsWith("data:image/png;base64,"))
                return ApiResponse<bool>.Fail("Only PNG data URLs are allowed.");
        }

        var signature = await _context.Set<UserSignature>()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (signature == null)
        {
            signature = new UserSignature
            {
                UserId = userId,
                DisplayName = user.FullName
            };
            _context.Add(signature);
        }
        else
        {
            signature.DisplayName = user.FullName; // ensure updated
        }

        signature.SignatureDrawnData = dto.SignatureDrawnData;
        signature.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated their signature.", userId);

        return ApiResponse<bool>.Success(true, "Signature saved successfully");
    }
}
