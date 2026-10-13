using AlFalah.Application.Duty;
namespace AlFalah.Infrastructure.Data.Seeders;
public static class DutyPermissionCatalog
{
    public static readonly (string Name, string Group, string DescAr, string DescEn)[] All =
    [
        (DutyPermissions.View, "Duty", "عرض جدول المناوبة", "View duty schedule"),
        (DutyPermissions.Manage, "Duty", "إدارة المناوبة", "Manage duty")
    ];
}
