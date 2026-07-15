using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Schools;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

public sealed class SchoolLocationService : ISchoolLocationService
{
    private readonly ISchoolLocationRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public SchoolLocationService(ISchoolLocationRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SchoolLocationDto>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _repository.Query()
            .Where(x => x.IsActive)
            .OrderBy(x => x.RegionNameAr)
            .ThenBy(x => x.NameAr)
            .Select(x => new SchoolLocationDto(
                x.Id,
                x.NameAr,
                x.NameEn,
                x.RegionNameAr,
                x.RegionNameEn,
                x.Latitude,
                x.Longitude))
            .ToListAsync(cancellationToken);

    public async Task<SchoolLocationDto> CreateAsync(
        SchoolLocationCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsGlobalAdmin())
            throw new UnauthorizedSchoolAccessException("إضافة مواقع المدارس متاحة للمدير العام ومدير النظام فقط.");

        var nameAr = request.NameAr.Trim();
        var regionAr = request.RegionNameAr.Trim();
        var duplicate = await _repository.Query()
            .AnyAsync(x => x.NameAr == nameAr && x.RegionNameAr == regionAr, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("هذا الموقع مسجل بالفعل ضمن المنطقة المحددة.");

        var location = new SchoolLocation
        {
            NameAr = nameAr,
            NameEn = Normalize(request.NameEn),
            RegionNameAr = regionAr,
            RegionNameEn = Normalize(request.RegionNameEn),
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        await _repository.AddAsync(location, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return ToDto(location);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SchoolLocationDto ToDto(SchoolLocation location) => new(
        location.Id,
        location.NameAr,
        location.NameEn,
        location.RegionNameAr,
        location.RegionNameEn,
        location.Latitude,
        location.Longitude);
}
