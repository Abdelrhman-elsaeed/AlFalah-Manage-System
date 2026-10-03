using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Data.Seeders;

public static class StoragePermissionCatalog
{
    public static readonly (string Name, string Group, string DescAr, string DescEn)[] All =
    [
        (PermissionNames.StorageViewSchool, "Storage", "عرض مكتبة المدرسة", "View school storage"),
        (PermissionNames.StorageManageSchool, "Storage", "إدارة مكتبة المدرسة", "Manage school storage"),
        (PermissionNames.StorageReviewEvidence, "Storage", "مراجعة الشواهد", "Review evidence"),
        (PermissionNames.StorageViewOwn, "Storage", "عرض ملفاتي", "View own storage"),
        (PermissionNames.StorageManageOwn, "Storage", "إدارة ملفاتي", "Manage own storage"),
        (PermissionNames.StorageViewArchive, "Storage", "عرض أرشيف الزيارات", "View visit archive"),
        (PermissionNames.StorageRetryArchive, "Storage", "إعادة محاولة الأرشفة", "Retry visit archive"),
        (PermissionNames.StorageDelegate, "Storage", "تفويض إدارة التخزين", "Delegate school storage")
    ];
}
