using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds the database with initial roles, permissions, super admin user,
/// and optional development sample data.
/// </summary>
public class DatabaseSeeder
{
    private readonly AlFalahDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AlFalahDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");

        await SeedRolesAsync();
        await SeedPermissionsAsync();
        await SyncRolePermissionsAsync();
        await SeedRubricAsync();
        await RetirePlaceholderStandardsAsync();

        _logger.LogInformation("Database seeding completed.");
    }

    // ─── Roles ───────────────────────────────────────────────────────────────

    private async Task SeedRolesAsync()
    {
        var roles = new[]
        {
            new ApplicationRole
            {
                Name = RoleNames.SuperAdmin,
                NormalizedName = RoleNames.SuperAdmin.ToUpper(),
                DescriptionAr = "مدير النظام / المطور",
                DescriptionEn = "Super Administrator / Developer"
            },
            new ApplicationRole
            {
                Name = RoleNames.MainManager,
                NormalizedName = RoleNames.MainManager.ToUpper(),
                DescriptionAr = "مدير المدارس العام",
                DescriptionEn = "Main Manager"
            },
            new ApplicationRole
            {
                Name = RoleNames.SchoolManager,
                NormalizedName = RoleNames.SchoolManager.ToUpper(),
                DescriptionAr = "مدير المدرسة",
                DescriptionEn = "School Manager"
            },
            new ApplicationRole
            {
                Name = RoleNames.Secretary,
                NormalizedName = RoleNames.Secretary.ToUpper(),
                DescriptionAr = "سكرتير المدرسة",
                DescriptionEn = "School Secretary"
            },
            new ApplicationRole
            {
                Name = RoleNames.Moderator,
                NormalizedName = RoleNames.Moderator.ToUpper(),
                DescriptionAr = "مشرف",
                DescriptionEn = "Moderator"
            },
            new ApplicationRole
            {
                Name = RoleNames.Instructor,
                NormalizedName = RoleNames.Instructor.ToUpper(),
                DescriptionAr = "معلم",
                DescriptionEn = "Instructor"
            },
            new ApplicationRole
            {
                Name = RoleNames.Guardian,
                NormalizedName = RoleNames.Guardian.ToUpper(),
                DescriptionAr = "ولي الأمر",
                DescriptionEn = "Guardian"
            },
            new ApplicationRole
            {
                Name = RoleNames.StudentAffairsOfficer,
                NormalizedName = RoleNames.StudentAffairsOfficer.ToUpper(),
                DescriptionAr = "وكيل شؤون الطلاب",
                DescriptionEn = "Student Affairs Officer"
            },
            new ApplicationRole
            {
                Name = RoleNames.SocialWorker,
                NormalizedName = RoleNames.SocialWorker.ToUpper(),
                DescriptionAr = "الموجه الطلابي / الأخصائي الاجتماعي",
                DescriptionEn = "Social Worker"
            },
            new ApplicationRole
            {
                Name = RoleNames.SecurityGuard,
                NormalizedName = RoleNames.SecurityGuard.ToUpper(),
                DescriptionAr = "حارس الأمن / حارس المدرسة",
                DescriptionEn = "Security Guard"
            }
        };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role.Name!))
            {
                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                    _logger.LogInformation("Created role: {RoleName}", role.Name);
                else
                    _logger.LogError("Failed to create role {RoleName}: {Errors}", role.Name,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    // ─── Permissions ─────────────────────────────────────────────────────────

    private async Task SeedPermissionsAsync()
    {
        var allPermissions = GetAllPermissions();

        foreach (var (name, group, descAr, descEn) in allPermissions)
        {
            if (!await _context.Permissions.AnyAsync(p => p.Name == name))
            {
                _context.Permissions.Add(new Permission
                {
                    Name = name,
                    Group = group,
                    DescriptionAr = descAr,
                    DescriptionEn = descEn
                });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Permissions seeded.");
    }

    private static IEnumerable<(string Name, string Group, string DescAr, string DescEn)> GetAllPermissions()
    {
        return new[]
        {
            // School
            (PermissionNames.SchoolView,   "School",  "عرض المدارس",       "View Schools"),
            (PermissionNames.SchoolCreate, "School",  "إضافة مدرسة",       "Create School"),
            (PermissionNames.SchoolEdit,   "School",  "تعديل مدرسة",       "Edit School"),
            (PermissionNames.SchoolDelete, "School",  "حذف مدرسة",         "Delete School"),
            (PermissionNames.SchoolDisable,"School",  "تعطيل مدرسة",       "Disable School"),

            // User
            (PermissionNames.UserView,    "User",    "عرض المستخدمين",    "View Users"),
            (PermissionNames.UserCreate,  "User",    "إضافة مستخدم",      "Create User"),
            (PermissionNames.UserEdit,    "User",    "تعديل مستخدم",      "Edit User"),
            (PermissionNames.UserDelete,  "User",    "حذف مستخدم",        "Delete User"),

            // Role
            (PermissionNames.RoleView,   "Role",    "عرض الأدوار",        "View Roles"),
            (PermissionNames.RoleManage, "Role",    "إدارة الأدوار",      "Manage Roles"),

            // Instructor
            (PermissionNames.InstructorView,   "Instructor", "عرض المعلمين",   "View Instructors"),
            (PermissionNames.InstructorCreate, "Instructor", "إضافة معلم",     "Create Instructor"),
            (PermissionNames.InstructorEdit,   "Instructor", "تعديل معلم",     "Edit Instructor"),
            (PermissionNames.InstructorDelete, "Instructor", "حذف معلم",       "Delete Instructor"),

            // Visit
            (PermissionNames.VisitView,   "Visit",  "عرض الزيارات",      "View Visits"),
            (PermissionNames.VisitCreate, "Visit",  "إنشاء زيارة",       "Create Visit"),
            (PermissionNames.VisitEdit,   "Visit",  "تعديل زيارة",       "Edit Visit"),
            (PermissionNames.VisitDelete, "Visit",  "حذف زيارة",         "Delete Visit"),
            (PermissionNames.VisitSubmit, "Visit",  "تسليم زيارة",       "Submit Visit"),
            (PermissionNames.VisitApprove,"Visit",  "اعتماد زيارة",      "Approve Visit"),
            (PermissionNames.VisitReopen, "Visit",  "إعادة فتح زيارة",   "Reopen Visit"),

            // Report
            (PermissionNames.ReportView,    "Report", "عرض التقارير",      "View Reports"),
            (PermissionNames.ReportDownload,"Report", "تحميل التقارير",    "Download Reports"),
            (PermissionNames.ReportGenerate,"Report", "إنشاء تقرير",       "Generate Report"),
            (PermissionNames.ReportExport,  "Report", "تصدير التقارير",    "Export Reports"),

            // Rubric
            (PermissionNames.RubricView,  "Rubric", "عرض الأداة",        "View Rubric"),
            (PermissionNames.RubricManage,"Rubric", "إدارة الأداة",      "Manage Rubric"),

            // Plan
            (PermissionNames.PlanView,  "Plan", "عرض خطط التطوير",   "View Plans"),
            (PermissionNames.PlanCreate,"Plan", "إنشاء خطة تطوير",   "Create Plan"),
            (PermissionNames.PlanEdit,  "Plan", "تعديل خطة تطوير",   "Edit Plan"),
            (PermissionNames.PlanDelete,"Plan", "حذف خطة تطوير",     "Delete Plan"),

            // FollowUp
            (PermissionNames.FollowUpView,  "FollowUp", "عرض المتابعات",   "View Follow-ups"),
            (PermissionNames.FollowUpCreate,"FollowUp", "إضافة متابعة",    "Create Follow-up"),
            (PermissionNames.FollowUpEdit,  "FollowUp", "تعديل متابعة",    "Edit Follow-up"),
            (PermissionNames.FollowUpDelete,"FollowUp", "حذف متابعة",      "Delete Follow-up"),

            // Complaint
            (PermissionNames.ComplaintView,  "Complaint", "عرض الشكاوى",   "View Complaints"),
            (PermissionNames.ComplaintCreate,"Complaint", "تقديم شكوى",    "Create Complaint"),
            (PermissionNames.ComplaintManage,"Complaint", "إدارة الشكاوى", "Manage Complaints"),

            // Dashboard
            (PermissionNames.DashboardMainManager,  "Dashboard", "لوحة مدير المدارس العام", "Main Manager Dashboard"),
            (PermissionNames.DashboardSchoolManager,"Dashboard", "لوحة مدير المدرسة",       "School Manager Dashboard"),
            (PermissionNames.DashboardModerator,    "Dashboard", "لوحة المشرف",              "Moderator Dashboard"),
            (PermissionNames.DashboardInstructor,   "Dashboard", "لوحة المعلم",              "Instructor Dashboard"),

            // Settings
            (PermissionNames.SettingsView,  "Settings", "عرض الإعدادات",   "View Settings"),
            (PermissionNames.SettingsManage,"Settings", "إدارة الإعدادات", "Manage Settings"),

            // Audit
            (PermissionNames.AuditLogView, "Audit", "عرض سجل التدقيق",  "View Audit Log"),

            // Attendance
            (PermissionNames.AttendanceView,   "Attendance", "عرض حضوري", "View my attendance"),
            (PermissionNames.AttendanceManage, "Attendance", "إدارة الحضور", "Manage school attendance"),

            // Timetable
            (PermissionNames.TimetableView, "Timetable", "عرض الجدول المدرسي", "View school timetable"),
            (PermissionNames.TimetableManage, "Timetable", "إدارة الجدول المدرسي", "Manage school timetable"),
            (PermissionNames.TimetableDelegate, "Timetable", "تفويض إدارة الجدول", "Delegate timetable management"),

            // Parent surveys
            (PermissionNames.ParentSurveyManage, "ParentSurvey", "إدارة استبيانات أولياء الأمور", "Manage parent surveys"),

            // Student and guardian administration
            (PermissionNames.StudentManage, "Student", "إدارة الطلاب", "Manage students"),
            (PermissionNames.StudentView, "Student", "عرض الطلاب", "View students"),
            (PermissionNames.StudentCreate, "Student", "إضافة طالب", "Create students"),
            (PermissionNames.StudentEdit, "Student", "تعديل بيانات الطالب", "Edit students"),
            (PermissionNames.StudentArchive, "Student", "أرشفة الطالب", "Archive students"),
            (PermissionNames.StudentEnrollmentManage, "Student", "إدارة تسجيل الطلاب", "Manage student enrollment"),
            (PermissionNames.ClassroomManage, "Classroom", "إدارة الفصول", "Manage classrooms"),
            (PermissionNames.GuardianView, "Guardian", "عرض أولياء الأمور", "View guardians"),
            (PermissionNames.GuardianManage, "Guardian", "إدارة أولياء الأمور", "Manage guardians"),
            (PermissionNames.GuardianLinkStudent, "Guardian", "ربط أولياء الأمور بالطلاب", "Link guardians to students"),
            (PermissionNames.GuardianViewLinkedStudents, "Guardian", "عرض الطلاب المرتبطين بولي الأمر", "View own linked students"),

            // Student attendance and excuses
            (PermissionNames.AttendanceViewStudents, "Attendance", "عرض حضور الطلاب", "View student attendance"),
            (PermissionNames.AttendanceManageStudents, "Attendance", "إدارة حضور الطلاب", "Manage student attendance"),
            (PermissionNames.AttendanceSubmitExcuse, "Attendance", "تقديم عذر غياب", "Submit absence excuses"),
            (PermissionNames.AttendanceReviewExcuse, "Attendance", "مراجعة أعذار الغياب", "Review absence excuses"),
            (PermissionNames.AttendanceOverrideCorrection, "Attendance", "تصحيح الحضور المقفل", "Override locked attendance corrections"),
            (PermissionNames.MorningDelayView, "MorningDelay", "عرض التأخر الصباحي", "View morning delays"),
            (PermissionNames.MorningDelayManageReason, "MorningDelay", "إدارة أسباب التأخر الصباحي", "Manage morning-delay reasons"),
            (PermissionNames.BiometricImport, "Biometric", "استيراد ملف جهاز البصمة", "Import biometric attendance workbook"),
            (PermissionNames.NoorExport, "Noor", "تصدير تصحيحات الغياب لنظام نور", "Export Noor absence corrections"),

            // Teacher observations and recognition
            (PermissionNames.SessionDelayView, "SessionDelay", "عرض التأخر عن الحصص", "View session delays"),
            (PermissionNames.SessionDelayCreate, "SessionDelay", "تسجيل التأخر عن الحصة", "Create session delays"),
            (PermissionNames.SessionDelayCorrect, "SessionDelay", "تصحيح التأخر عن الحصة", "Correct session delays"),
            (PermissionNames.AcademicConcernView, "AcademicConcern", "عرض الملاحظات الأكاديمية", "View academic concerns"),
            (PermissionNames.AcademicConcernCreate, "AcademicConcern", "تسجيل ملاحظة أكاديمية", "Create academic concerns"),
            (PermissionNames.AcademicConcernManage, "AcademicConcern", "إدارة الملاحظات الأكاديمية", "Manage academic concerns"),
            (PermissionNames.BehaviorView, "Behavior", "عرض السلوك الطلابي", "View behavior incidents"),
            (PermissionNames.BehaviorCreate, "Behavior", "تسجيل واقعة سلوكية", "Create behavior incidents"),
            (PermissionNames.BehaviorManage, "Behavior", "إدارة الوقائع السلوكية", "Manage behavior incidents"),
            (PermissionNames.RecognitionView, "Recognition", "عرض التكريمات", "View recognitions"),
            (PermissionNames.RecognitionCreate, "Recognition", "تسجيل تكريم", "Create recognitions"),
            (PermissionNames.RecognitionManage, "Recognition", "إدارة التكريمات", "Manage recognitions"),
            (PermissionNames.RecognitionViewStatistics, "Recognition", "عرض إحصاءات التكريم", "View recognition statistics"),
            (PermissionNames.TeacherQuickActionView, "TeacherQuickAction", "عرض الإجراءات السريعة للمعلم", "View teacher quick actions"),
            (PermissionNames.TeacherQuickActionOverride, "TeacherQuickAction", "تجاوز نطاق الحصة الحالية", "Override current teacher timetable scope"),

            // Classroom-entry permits
            (PermissionNames.ClassroomEntryPermitView, "ClassroomEntryPermit", "عرض تصاريح دخول الفصل", "View classroom-entry permits"),
            (PermissionNames.ClassroomEntryPermitIssue, "ClassroomEntryPermit", "إصدار تصريح دخول الفصل", "Issue classroom-entry permits"),
            (PermissionNames.ClassroomEntryPermitAcknowledge, "ClassroomEntryPermit", "تأكيد استلام تصريح دخول الفصل", "Acknowledge classroom-entry permits"),
            (PermissionNames.ClassroomEntryPermitRevoke, "ClassroomEntryPermit", "إلغاء تصريح دخول الفصل", "Revoke classroom-entry permits"),

            // Gate passes
            (PermissionNames.GatePassView, "GatePass", "عرض تصاريح الخروج", "View gate passes"),
            (PermissionNames.GatePassViewOwn, "GatePass", "عرض طلبات الخروج الخاصة", "View own gate-pass requests"),
            (PermissionNames.GatePassRequest, "GatePass", "طلب تصريح خروج", "Request gate passes"),
            (PermissionNames.GatePassCancelOwn, "GatePass", "إلغاء طلب الخروج الخاص", "Cancel own gate-pass requests"),
            (PermissionNames.GatePassApprove, "GatePass", "اعتماد تصريح الخروج", "Approve gate passes"),
            (PermissionNames.GatePassReject, "GatePass", "رفض تصريح الخروج", "Reject gate passes"),
            (PermissionNames.GatePassAcknowledgeTeacher, "GatePass", "تأكيد المعلم لإشعار الخروج", "Acknowledge gate passes as teacher"),
            (PermissionNames.GatePassAcknowledgeSecurity, "GatePass", "تأكيد الأمن لتصريح الخروج", "Acknowledge gate passes as security"),
            (PermissionNames.GatePassExecute, "GatePass", "تنفيذ خروج الطالب", "Execute gate passes"),
            (PermissionNames.GatePassOverride, "GatePass", "تجاوز استثنائي لتصريح الخروج", "Override gate-pass exceptions"),
            (PermissionNames.GatePassViewAudit, "GatePass", "عرض سجل تدقيق تصاريح الخروج", "View gate-pass audit"),

            // Referrals, cases, and summons
            (PermissionNames.ReferralView, "Referral", "عرض الإحالات", "View referrals"),
            (PermissionNames.ReferralCreate, "Referral", "إنشاء إحالة", "Create referrals"),
            (PermissionNames.ReferralAssign, "Referral", "تعيين الإحالات", "Assign referrals"),
            (PermissionNames.ReferralManage, "Referral", "إدارة الإحالات والحالات", "Manage referrals and cases"),
            (PermissionNames.ReferralViewConfidential, "Referral", "عرض ملاحظات الحالات السرية", "View confidential case notes"),
            (PermissionNames.SummonView, "Summon", "عرض الاستدعاءات", "View summons"),
            (PermissionNames.SummonCreate, "Summon", "إنشاء استدعاء", "Create summons"),
            (PermissionNames.SummonSchedule, "Summon", "جدولة الاستدعاء", "Schedule summons"),
            (PermissionNames.SummonMarkAttended, "Summon", "تسجيل حضور الاستدعاء", "Mark summons attended"),
            (PermissionNames.SummonStartObservation, "Summon", "بدء فترة الملاحظة", "Start summon observation"),
            (PermissionNames.SummonMarkImproved, "Summon", "تسجيل تحسن الحالة", "Mark summons improved"),
            (PermissionNames.SummonViewHistory, "Summon", "عرض سجل الاستدعاء", "View summon history"),
            (PermissionNames.SummonReviewAutomationImpact, "Summon", "مراجعة أثر إعادة احتساب الاستدعاء", "Review summons affected by automation recalculation"),

            // Messaging and office hours
            (PermissionNames.MessagingViewOwn, "Messaging", "عرض المحادثات الخاصة", "View own message threads"),
            (PermissionNames.MessagingSend, "Messaging", "إرسال الرسائل", "Send messages"),
            (PermissionNames.MessagingStartGuardianTeacher, "Messaging", "بدء محادثة بين ولي الأمر والمعلم", "Start guardian-teacher threads"),
            (PermissionNames.MessagingStartGuardianAdministration, "Messaging", "بدء محادثة مع شؤون الطلاب", "Start guardian-administration threads"),
            (PermissionNames.MessagingCloseThread, "Messaging", "إغلاق المحادثة", "Close message threads"),
            (PermissionNames.MessagingViewAudit, "Messaging", "عرض سجل تدقيق الرسائل", "View messaging audit"),
            (PermissionNames.OfficeHoursView, "OfficeHours", "عرض الساعات المكتبية", "View office hours"),
            (PermissionNames.OfficeHoursManageOwn, "OfficeHours", "إدارة الساعات المكتبية الخاصة", "Manage own office hours"),
            (PermissionNames.OfficeHoursManageSchool, "OfficeHours", "إدارة الساعات المكتبية للمدرسة", "Manage school office hours"),

            // Settings, automation, notifications, and dashboards
            (PermissionNames.StudentAffairsSettingsView, "StudentAffairsSettings", "عرض إعدادات شؤون الطلاب", "View Student Affairs settings"),
            (PermissionNames.StudentAffairsSettingsManage, "StudentAffairsSettings", "إدارة إعدادات شؤون الطلاب", "Manage Student Affairs settings"),
            (PermissionNames.AutomationView, "Automation", "عرض قواعد الأتمتة وسجل التشغيل", "View automation rules and trigger history"),
            (PermissionNames.AutomationRetry, "Automation", "إعادة محاولة إجراء آلي فاشل", "Retry failed automated actions"),
            (PermissionNames.NotificationViewOwn, "Notification", "عرض الإشعارات الخاصة", "View own notifications"),
            (PermissionNames.NotificationApproveDispatch, "Notification", "اعتماد إرسال إشعار ولي الأمر", "Approve guardian notification dispatch"),
            (PermissionNames.NotificationSuppressDispatch, "Notification", "منع إرسال إشعار ولي الأمر", "Suppress guardian notification dispatch"),
            (PermissionNames.NotificationViewDelivery, "Notification", "عرض حالة تسليم الإشعارات", "View notification delivery status"),
            (PermissionNames.StudentAffairsDashboardTeacher, "StudentAffairsDashboard", "لوحة شؤون الطلاب للمعلم", "Teacher Student Affairs dashboard"),
            (PermissionNames.StudentAffairsDashboardOfficer, "StudentAffairsDashboard", "لوحة عمليات شؤون الطلاب", "Student Affairs Officer dashboard"),
            (PermissionNames.StudentAffairsDashboardSocialWorker, "StudentAffairsDashboard", "لوحة الأخصائي الاجتماعي", "Social Worker dashboard"),
            (PermissionNames.StudentAffairsDashboardSecurity, "StudentAffairsDashboard", "لوحة بوابة الأمن", "Security gate dashboard"),
            (PermissionNames.StudentAffairsDashboardGuardian, "StudentAffairsDashboard", "لوحة ولي الأمر", "Guardian Student Affairs dashboard"),
            (PermissionNames.StudentAffairsDashboardSchoolOversight, "StudentAffairsDashboard", "لوحة الإشراف المدرسي على شؤون الطلاب", "School Student Affairs oversight dashboard"),
        };
    }

    // ─── Role → Permission mapping ────────────────────────────────────────────

    /// <summary>
    /// Canonical role→permission map, derived from <c>docs/03-ROLES-AND-PERMISSIONS.md</c>.
    /// Used by <see cref="SyncRolePermissionsAsync"/> to add missing mappings AND
    /// remove stale ones. The map itself lives in code so the security boundary
    /// is auditable.
    ///
    /// NOTE (D-24): School Manager retains <c>User.*</c> perms because docs/03 §3
    /// grants him "Manage teachers/instructors; add/edit" inside his school. The
    /// runtime school-scope filter in <c>UserService</c> / <c>UserSchoolRoleService</c>
    /// is what enforces that he sees ONLY his own school's staff — not a permission
    /// removal. <c>Add School Manager</c> (creating a SchoolManager user) remains
    /// a Main Manager responsibility (see docs/03 §2), which the filter enforces
    /// because the SchoolManager role cannot call POST /users with role=SchoolManager
    /// for a school that is not his own.
    /// </summary>
    private static Dictionary<string, IEnumerable<string>> GetRolePermissionMap()
    {
        return new Dictionary<string, IEnumerable<string>>
        {
            [RoleNames.SuperAdmin] = GetAllPermissions().Select(p => p.Name),

            [RoleNames.MainManager] = new[]
            {
                PermissionNames.SchoolView, PermissionNames.SchoolCreate,
                PermissionNames.SchoolEdit, PermissionNames.SchoolDelete, PermissionNames.SchoolDisable,
                PermissionNames.UserView, PermissionNames.UserCreate, PermissionNames.UserEdit, PermissionNames.UserDelete,
                PermissionNames.RoleView, PermissionNames.RoleManage,
                PermissionNames.InstructorView, PermissionNames.InstructorCreate, PermissionNames.InstructorEdit, PermissionNames.InstructorDelete,
                PermissionNames.VisitView, PermissionNames.VisitCreate, PermissionNames.VisitEdit, PermissionNames.VisitDelete,
                PermissionNames.VisitSubmit, PermissionNames.VisitApprove, PermissionNames.VisitReopen,
                PermissionNames.ReportView, PermissionNames.ReportExport,
                PermissionNames.RubricView, PermissionNames.RubricManage,
                PermissionNames.PlanView,
                PermissionNames.DashboardMainManager,
                PermissionNames.SettingsView, PermissionNames.SettingsManage,
                PermissionNames.AttendanceView, PermissionNames.AttendanceManage,
                PermissionNames.TimetableView, PermissionNames.TimetableManage, PermissionNames.TimetableDelegate,
                PermissionNames.RecognitionViewStatistics,
                PermissionNames.StudentAffairsSettingsView,
                PermissionNames.AutomationView,
                PermissionNames.NotificationViewDelivery,
            },

            [RoleNames.SchoolManager] = new[]
            {
                PermissionNames.InstructorView, PermissionNames.InstructorCreate,
                PermissionNames.InstructorEdit, PermissionNames.InstructorDelete,
                PermissionNames.VisitView, PermissionNames.VisitCreate,
                PermissionNames.VisitEdit, PermissionNames.VisitDelete,
                PermissionNames.VisitSubmit, PermissionNames.VisitApprove, PermissionNames.VisitReopen,
                PermissionNames.ReportView, PermissionNames.ReportDownload,
                PermissionNames.ReportGenerate, PermissionNames.ReportExport,
                PermissionNames.PlanView, PermissionNames.PlanCreate,
                PermissionNames.PlanEdit, PermissionNames.PlanDelete,
                PermissionNames.FollowUpView, PermissionNames.FollowUpCreate,
                PermissionNames.FollowUpEdit, PermissionNames.FollowUpDelete,
                PermissionNames.ComplaintView, PermissionNames.ComplaintManage,
                PermissionNames.UserView, PermissionNames.UserCreate, PermissionNames.UserEdit, PermissionNames.UserDelete,
                PermissionNames.DashboardSchoolManager,
                PermissionNames.SettingsView, PermissionNames.SettingsManage,
                PermissionNames.RubricView,  // MOD-1: all authenticated roles get Rubric.View
                PermissionNames.AttendanceView,
                PermissionNames.ParentSurveyManage,
                PermissionNames.TimetableView, PermissionNames.TimetableManage, PermissionNames.TimetableDelegate,
                PermissionNames.StudentView, PermissionNames.StudentCreate,
                PermissionNames.StudentEdit, PermissionNames.StudentArchive, PermissionNames.StudentManage,
                PermissionNames.StudentEnrollmentManage,
                PermissionNames.GuardianView, PermissionNames.GuardianManage, PermissionNames.GuardianLinkStudent,
                PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceOverrideCorrection,
                PermissionNames.RecognitionViewStatistics,
                PermissionNames.ClassroomEntryPermitView, PermissionNames.ClassroomEntryPermitIssue,
                PermissionNames.ClassroomEntryPermitRevoke,
                PermissionNames.GatePassView, PermissionNames.GatePassApprove,
                PermissionNames.GatePassReject, PermissionNames.GatePassOverride, PermissionNames.GatePassViewAudit,
                PermissionNames.ReferralView,
                PermissionNames.SummonView, PermissionNames.SummonViewHistory,
                PermissionNames.MessagingViewAudit,
                PermissionNames.OfficeHoursView, PermissionNames.OfficeHoursManageSchool,
                PermissionNames.StudentAffairsSettingsView,
                PermissionNames.AutomationView, PermissionNames.NotificationViewDelivery,
                PermissionNames.StudentAffairsDashboardSchoolOversight,
            },

            [RoleNames.Moderator] = new[]
            {
                // P0.3: narrow teacher-directory/profile read. Deliberately do
                // not grant User.View/User.Edit/User.Delete (no broad directory).
                PermissionNames.InstructorView,
                PermissionNames.VisitView, PermissionNames.VisitCreate,
                PermissionNames.VisitEdit, PermissionNames.VisitDelete, PermissionNames.VisitSubmit,
                PermissionNames.ReportView, PermissionNames.ReportDownload, PermissionNames.ReportGenerate,
                PermissionNames.PlanView, PermissionNames.PlanCreate, PermissionNames.PlanEdit, PermissionNames.PlanDelete,
                PermissionNames.FollowUpView, PermissionNames.FollowUpCreate,
                PermissionNames.FollowUpEdit, PermissionNames.FollowUpDelete,
                PermissionNames.DashboardModerator,
                PermissionNames.RubricView,  // MOD-1: all authenticated roles get Rubric.View
                PermissionNames.AttendanceView,
                PermissionNames.ParentSurveyManage,
                PermissionNames.TimetableView,
            },

            [RoleNames.Instructor] = new[]
            {
                PermissionNames.ReportView, PermissionNames.ReportDownload,
                PermissionNames.ComplaintCreate, PermissionNames.ComplaintView,
                PermissionNames.DashboardInstructor,
                PermissionNames.AttendanceView,
                PermissionNames.TimetableView,
                PermissionNames.StudentView,
                PermissionNames.SessionDelayView, PermissionNames.SessionDelayCreate,
                PermissionNames.AcademicConcernView, PermissionNames.AcademicConcernCreate,
                PermissionNames.BehaviorView, PermissionNames.BehaviorCreate,
                PermissionNames.RecognitionView, PermissionNames.RecognitionCreate,
                PermissionNames.TeacherQuickActionView,
                PermissionNames.ClassroomEntryPermitView, PermissionNames.ClassroomEntryPermitAcknowledge,
                PermissionNames.GatePassView, PermissionNames.GatePassAcknowledgeTeacher,
                PermissionNames.MessagingViewOwn, PermissionNames.MessagingSend, PermissionNames.MessagingCloseThread,
                PermissionNames.OfficeHoursView, PermissionNames.OfficeHoursManageOwn,
                PermissionNames.NotificationViewOwn,
                PermissionNames.StudentAffairsDashboardTeacher,
            },

            [RoleNames.Secretary] = new[]
            {
                PermissionNames.AttendanceView, PermissionNames.AttendanceManage,
                PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceManageStudents,
                PermissionNames.ClassroomManage, PermissionNames.StudentManage,
                PermissionNames.BiometricImport,
            },

            [RoleNames.StudentAffairsOfficer] = new[]
            {
                PermissionNames.StudentView, PermissionNames.StudentCreate,
                PermissionNames.StudentEdit, PermissionNames.StudentArchive, PermissionNames.StudentManage,
                PermissionNames.StudentEnrollmentManage,
                PermissionNames.GuardianView, PermissionNames.GuardianManage, PermissionNames.GuardianLinkStudent,
                PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceReviewExcuse,
                PermissionNames.BiometricImport, PermissionNames.NoorExport,
                PermissionNames.MorningDelayView, PermissionNames.MorningDelayManageReason,
                PermissionNames.SessionDelayView, PermissionNames.SessionDelayCorrect,
                PermissionNames.AcademicConcernView, PermissionNames.AcademicConcernManage,
                PermissionNames.BehaviorView, PermissionNames.BehaviorManage,
                PermissionNames.RecognitionView, PermissionNames.RecognitionManage,
                PermissionNames.RecognitionViewStatistics,
                PermissionNames.ClassroomEntryPermitView, PermissionNames.ClassroomEntryPermitIssue,
                PermissionNames.ClassroomEntryPermitRevoke,
                PermissionNames.GatePassView, PermissionNames.GatePassApprove,
                PermissionNames.GatePassReject, PermissionNames.GatePassViewAudit,
                PermissionNames.ReferralView, PermissionNames.ReferralCreate, PermissionNames.ReferralAssign,
                PermissionNames.SummonView, PermissionNames.SummonViewHistory,
                PermissionNames.SummonReviewAutomationImpact,
                PermissionNames.MessagingViewOwn, PermissionNames.MessagingSend,
                PermissionNames.MessagingCloseThread, PermissionNames.MessagingViewAudit,
                PermissionNames.StudentAffairsSettingsView, PermissionNames.StudentAffairsSettingsManage,
                PermissionNames.AutomationView, PermissionNames.AutomationRetry,
                PermissionNames.NotificationViewOwn, PermissionNames.NotificationApproveDispatch,
                PermissionNames.NotificationSuppressDispatch, PermissionNames.NotificationViewDelivery,
                PermissionNames.StudentAffairsDashboardOfficer,
            },

            [RoleNames.SocialWorker] = new[]
            {
                PermissionNames.StudentView, PermissionNames.GuardianView,
                PermissionNames.AttendanceViewStudents, PermissionNames.MorningDelayView,
                PermissionNames.SessionDelayView, PermissionNames.AcademicConcernView,
                PermissionNames.BehaviorView, PermissionNames.RecognitionView,
                PermissionNames.ClassroomEntryPermitView, PermissionNames.GatePassView,
                PermissionNames.ReferralView, PermissionNames.ReferralManage,
                PermissionNames.ReferralViewConfidential,
                PermissionNames.SummonView, PermissionNames.SummonCreate,
                PermissionNames.SummonSchedule, PermissionNames.SummonMarkAttended,
                PermissionNames.SummonStartObservation, PermissionNames.SummonMarkImproved,
                PermissionNames.SummonViewHistory,
                PermissionNames.MessagingViewOwn, PermissionNames.MessagingSend,
                PermissionNames.MessagingCloseThread,
                PermissionNames.NotificationViewOwn, PermissionNames.NotificationViewDelivery,
                PermissionNames.StudentAffairsDashboardSocialWorker,
            },

            [RoleNames.SecurityGuard] = new[]
            {
                PermissionNames.GatePassView,
                PermissionNames.GatePassAcknowledgeSecurity,
                PermissionNames.GatePassExecute,
                PermissionNames.StudentAffairsDashboardSecurity,
            },

            [RoleNames.Guardian] = new[]
            {
                PermissionNames.GuardianViewLinkedStudents,
                PermissionNames.AttendanceViewStudents, PermissionNames.AttendanceSubmitExcuse,
                PermissionNames.MorningDelayView, PermissionNames.SessionDelayView,
                PermissionNames.AcademicConcernView, PermissionNames.BehaviorView,
                PermissionNames.RecognitionView, PermissionNames.ClassroomEntryPermitView,
                PermissionNames.GatePassViewOwn, PermissionNames.GatePassRequest, PermissionNames.GatePassCancelOwn,
                PermissionNames.MessagingViewOwn, PermissionNames.MessagingSend,
                PermissionNames.MessagingStartGuardianTeacher,
                PermissionNames.MessagingStartGuardianAdministration,
                PermissionNames.MessagingCloseThread, PermissionNames.OfficeHoursView,
                PermissionNames.NotificationViewOwn,
                PermissionNames.StudentAffairsDashboardGuardian,
            },
        };
    }

    /// <summary>
    /// Idempotent two-way sync: adds missing RolePermission rows for each role AND
    /// removes any rows no longer in the canonical map. Safe to run on every boot;
    /// the RolePermissions table is small enough to reconcile on every seed run.
    /// </summary>
    private async Task SyncRolePermissionsAsync()
    {
        var rolePermissionMap = GetRolePermissionMap();
        var totalAdded = 0;
        var totalRemoved = 0;

        foreach (var (roleName, permissionNames) in rolePermissionMap)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            var desiredPermissionIds = new List<int>();
            foreach (var permName in permissionNames)
            {
                var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permName);
                if (permission != null) desiredPermissionIds.Add(permission.Id);
            }

            var existingRows = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync();

            var existingPermissionIds = existingRows.Select(rp => rp.PermissionId).ToHashSet();

            // Add missing
            foreach (var permId in desiredPermissionIds)
            {
                if (!existingPermissionIds.Contains(permId))
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permId
                    });
                    totalAdded++;
                }
            }

            // Remove stale (anything the canonical map no longer requires)
            foreach (var row in existingRows)
            {
                if (!desiredPermissionIds.Contains(row.PermissionId))
                {
                    _context.RolePermissions.Remove(row);
                    totalRemoved++;
                }
            }
        }

        if (totalAdded > 0 || totalRemoved > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "RolePermissions synced: +{Added} added, -{Removed} removed (canonical map in DatabaseSeeder.GetRolePermissionMap).",
                totalAdded, totalRemoved);
        }
        else
        {
            _logger.LogInformation("RolePermissions already in sync with canonical map.");
        }
    }

    // ─── Super Admin user ─────────────────────────────────────────────────────

    // Development-only credentials. Passwords are always passed through ASP.NET
    // Core Identity; these values are never written to the database in plaintext.
    private const string SuperAdminPassword = "AlFalah@SuperAdmin2024!";
    private const string MainManagerPassword = "AlFalah@MainManager2024!";
    private const string SchoolManagerPassword = "AlFalah@Manager2024!";
    private const string SecretaryPassword = "AlFalah@Secretary2024!";
    private const string ModeratorPassword = "AlFalah@Moderator2024!";
    private const string InstructorPassword = "AlFalah@Instructor2024!";

    private async Task<School> EnsureSampleSchoolAsync()
    {
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s =>
                s.Name == "مدرسة الفلاح النموذجية"
                && s.Stage == SchoolStage.Primary
                && s.City == "الرياض"
                && s.LocationDetails == "حي النخيل");

        if (school == null)
        {
            school = new School
            {
                Name = "مدرسة الفلاح النموذجية",
                Stage = SchoolStage.Primary,
                City = "الرياض",
                LocationDetails = "حي النخيل",
                IsActive = true
            };
            _context.Schools.Add(school);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Sample school created: {SchoolName} (Id: {SchoolId})", school.Name, school.Id);
        }
        else if (school.IsDeleted || !school.IsActive)
        {
            school.IsDeleted = false;
            school.DeletedAt = null;
            school.DeletedByUserId = null;
            school.IsActive = true;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Sample school restored: {SchoolName} (Id: {SchoolId})", school.Name, school.Id);
        }

        return school;
    }

    private async Task RetirePlaceholderStandardsAsync()
    {
        var placeholders = await _context.RubricStandards
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted &&
                (s.TextAr.Contains("كوكو") || s.TextAr.Contains("بوبو") ||
                 s.TextAr.ToLower().Contains("koko") ||
                 s.TextAr.ToLower().Contains("bobo")))
            .ToListAsync();

        if (placeholders.Count == 0) return;
        foreach (var standard in placeholders)
        {
            standard.IsDeleted = true;
            standard.DeletedAt = DateTimeOffset.UtcNow;
            standard.DeletedByUserId = "system-rubric-cleanup";
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Retired {Count} placeholder rubric standards.", placeholders.Count);
    }

    private async Task SeedDevAccountsAsync(School sampleSchool)
    {
        var superAdmin = await EnsureDevUserAsync(
            "superadmin", "superadmin@alfalah.edu.sa", "مدير", "النظام",
            RoleNames.SuperAdmin, SuperAdminPassword);

        await EnsureDevUserAsync(
            "main_manager_1", "mainmanager1@alfalah.edu.sa", "مدير", "المدارس العام",
            RoleNames.MainManager, MainManagerPassword);

        var schoolManager = await EnsureDevUserAsync(
            "school_manager_1", "manager1@alfalah.edu.sa", "أحمد", "العمري",
            RoleNames.SchoolManager, SchoolManagerPassword);

        var secretary = await EnsureDevUserAsync(
            "secretary_1", "secretary1@alfalah.edu.sa", "Secretary", "Test",
            RoleNames.Secretary, SecretaryPassword);

        var moderator = await EnsureDevUserAsync(
            "moderator_1", "moderator1@alfalah.edu.sa", "سارة", "الحربي",
            RoleNames.Moderator, ModeratorPassword);

        var instructor = await EnsureDevUserAsync(
            "instructor_1", "instructor1@alfalah.edu.sa", "معلم", "تجريبي",
            RoleNames.Instructor, InstructorPassword);

        await EnsureSchoolAssignmentAsync(schoolManager, sampleSchool, RoleNames.SchoolManager);
        await EnsureSchoolAssignmentAsync(secretary, sampleSchool, RoleNames.Secretary);
        await EnsureSchoolAssignmentAsync(moderator, sampleSchool, RoleNames.Moderator);
        await EnsureSchoolAssignmentAsync(instructor, sampleSchool, RoleNames.Instructor);

        if (sampleSchool.ManagerUserId != schoolManager.Id)
        {
            sampleSchool.ManagerUserId = schoolManager.Id;
            await _context.SaveChangesAsync();
        }

        await EnsureInstructorProfileAsync(instructor, sampleSchool);

        _logger.LogInformation(
            "Development accounts ensured: {SuperAdmin}, {MainManager}, {SchoolManager}, {Moderator}, {Instructor}",
            superAdmin.UserName, "main_manager_1", schoolManager.UserName, moderator.UserName, instructor.UserName);
    }

    private async Task<ApplicationUser> EnsureDevUserAsync(
        string username,
        string email,
        string firstName,
        string lastName,
        string roleName,
        string password)
    {
        var normalizedUsername = username.ToUpperInvariant();
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUsername);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                PreferredLanguage = "ar",
                IsActive = true
            };

            EnsureIdentitySuccess(
                await _userManager.CreateAsync(user, password),
                $"create {username}");
        }
        else
        {
            user.Email = email;
            user.EmailConfirmed = true;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.PreferredLanguage = "ar";
            user.IsActive = true;
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedByUserId = null;
            EnsureIdentitySuccess(await _userManager.UpdateAsync(user), $"update {username}");
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            if (!string.IsNullOrEmpty(user.PasswordHash))
                EnsureIdentitySuccess(await _userManager.RemovePasswordAsync(user), $"reset password for {username}");

            EnsureIdentitySuccess(await _userManager.AddPasswordAsync(user, password), $"set password for {username}");
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
            EnsureIdentitySuccess(await _userManager.AddToRoleAsync(user, roleName), $"assign {roleName} to {username}");

        return user;
    }

    private async Task EnsureSchoolAssignmentAsync(ApplicationUser user, School school, string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName)
            ?? throw new InvalidOperationException($"Seed role '{roleName}' was not found.");

        var assignment = await _context.UserSchoolRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.SchoolId == school.Id && x.RoleId == role.Id);

        if (assignment == null)
        {
            _context.UserSchoolRoles.Add(new UserSchoolRole
            {
                UserId = user.Id,
                SchoolId = school.Id,
                RoleId = role.Id,
                IsActive = true,
                IsDeleted = false
            });
        }
        else
        {
            assignment.IsActive = true;
            assignment.IsDeleted = false;
            assignment.DeletedAt = null;
            assignment.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureInstructorProfileAsync(ApplicationUser instructor, School school)
    {
        var profile = await _context.InstructorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == instructor.Id);

        if (profile == null)
        {
            _context.InstructorProfiles.Add(new InstructorProfile
            {
                UserId = instructor.Id,
                SchoolId = school.Id,
                Stage = school.Stage,
                SubjectSpecialization = "الرياضيات",
                EmployeeNumber = "DEV-INSTRUCTOR-1",
                IsActive = true,
                IsDeleted = false
            });
        }
        else
        {
            profile.SchoolId = school.Id;
            profile.Stage = school.Stage;
            profile.IsActive = true;
            profile.IsDeleted = false;
            profile.DeletedAt = null;
            profile.DeletedByUserId = null;
        }

        await _context.SaveChangesAsync();
    }

    private static void EnsureIdentitySuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity seed operation failed ({operation}): {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        const string superAdminUsername = "superadmin";
        const string superAdminEmail = "superadmin@alfalah.edu.sa";

        var user = await _userManager.FindByNameAsync(superAdminUsername);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = superAdminUsername,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FirstName = "مدير",
                LastName = "النظام",
                PreferredLanguage = "ar",
                IsActive = true
            };

            // Password is set from environment/config in production. Use env var DEV_SUPER_ADMIN_PASSWORD or fallback.
            var password = Environment.GetEnvironmentVariable("DEV_SUPER_ADMIN_PASSWORD")
                           ?? "AlFalah@SuperAdmin2024!";

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create SuperAdmin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            await _userManager.AddToRoleAsync(user, RoleNames.SuperAdmin);
            _logger.LogInformation("SuperAdmin user created with username: {Username}", superAdminUsername);
        }
    }

    // ─── Sample dev data ─────────────────────────────────────────────────────

    private async Task SeedSampleDataAsync()
    {
        // Only seed sample data if no schools exist
        if (await _context.Schools.AnyAsync()) return;

        // Create a sample school
        var school = new School
        {
            Name = "مدرسة الفلاح النموذجية",
            Stage = Domain.Enums.SchoolStage.Primary,
            City = "الرياض",
            LocationDetails = "حي النخيل",
            IsActive = true
        };
        _context.Schools.Add(school);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sample school created: {SchoolName} (Id: {SchoolId})", school.Name, school.Id);

        // Create a sample School Manager user
        const string managerUsername = "school_manager_1";
        if (await _userManager.FindByNameAsync(managerUsername) == null)
        {
            var managerUser = new ApplicationUser
            {
                UserName = managerUsername,
                Email = "manager1@alfalah.edu.sa",
                EmailConfirmed = true,
                FirstName = "أحمد",
                LastName = "العمري",
                PreferredLanguage = "ar",
                IsActive = true
            };

            var password = Environment.GetEnvironmentVariable("DEV_SAMPLE_PASSWORD")
                           ?? "AlFalah@Manager2024!";

            var result = await _userManager.CreateAsync(managerUser, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(managerUser, RoleNames.SchoolManager);

                // Assign to the sample school
                var managerRole = await _roleManager.FindByNameAsync(RoleNames.SchoolManager);
                _context.UserSchoolRoles.Add(new UserSchoolRole
                {
                    UserId = managerUser.Id,
                    SchoolId = school.Id,
                    RoleId = managerRole!.Id,
                    IsActive = true
                });

                // Update school manager
                school.ManagerUserId = managerUser.Id;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sample SchoolManager created: {Username}", managerUsername);
            }
        }

        // Create a sample Moderator user
        const string moderatorUsername = "moderator_1";
        if (await _userManager.FindByNameAsync(moderatorUsername) == null)
        {
            var moderatorUser = new ApplicationUser
            {
                UserName = moderatorUsername,
                Email = "moderator1@alfalah.edu.sa",
                EmailConfirmed = true,
                FirstName = "سارة",
                LastName = "الحربي",
                PreferredLanguage = "ar",
                IsActive = true
            };

            var password = Environment.GetEnvironmentVariable("DEV_SAMPLE_PASSWORD")
                           ?? "AlFalah@Moderator2024!";

            var result = await _userManager.CreateAsync(moderatorUser, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(moderatorUser, RoleNames.Moderator);

                var moderatorRole = await _roleManager.FindByNameAsync(RoleNames.Moderator);
                _context.UserSchoolRoles.Add(new UserSchoolRole
                {
                    UserId = moderatorUser.Id,
                    SchoolId = school.Id,
                    RoleId = moderatorRole!.Id,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                _logger.LogInformation("Sample Moderator created: {Username}", moderatorUsername);
            }
        }
    }

    // ─── Rubric seed (Phase 3) ────────────────────────────────────────────────

    /// <summary>
    /// Seeds RubricVersion 1 (IsActive=true) with exactly 5 domains and 25 standards.
    /// Arabic text is VERBATIM from the spec. Distribution: D1=6, D2=4, D3=6, D4=3, D5=6.
    /// Codes: D1..D5 for domains; D{n}-S{m} for standards.
    /// Guard: runs only if no RubricVersion rows exist (idempotent).
    /// </summary>
    private async Task SeedRubricAsync()
    {
        if (await _context.RubricVersions.IgnoreQueryFilters().AnyAsync()) return;

        var version = new RubricVersion
        {
            VersionNumber = 1,
            IsActive = true,
            Notes = "الإصدار الأول — البيانات الأولية من دليل التقييم",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // ── D1: بيئة التعلم (6 standards) ──────────────────────────────────
        var d1 = new RubricDomain { Code = "D1", NameAr = "بيئة التعلم", SortOrder = 1 };
        d1.Standards.Add(new RubricStandard { Code = "D1-S1", SortOrder = 1, TextAr = "تنفذ المدرسة برامج وأنشطة لتعزيز القيم الإسلامية والهوية الوطنية لدى المتعلمين." });
        d1.Standards.Add(new RubricStandard { Code = "D1-S2", SortOrder = 2, TextAr = "تنفذ المدرسة إجراءات تضمن مناخاً آمناً للتعلم والنمو نفسياً واجتماعياً." });
        d1.Standards.Add(new RubricStandard { Code = "D1-S3", SortOrder = 3, TextAr = "يتوفر في بيئة التعلم مصادر وأنشطة متنوعة تلبي احتياجات المتعلمين." });
        d1.Standards.Add(new RubricStandard { Code = "D1-S4", SortOrder = 4, TextAr = "يدار الوقت في بيئة التعلم بفاعلية لدعم التعلم." });
        d1.Standards.Add(new RubricStandard { Code = "D1-S5", SortOrder = 5, TextAr = "تتاح للمتعلمين فرص متكافئة في الأنشطة والمناقشة الصفية." });
        d1.Standards.Add(new RubricStandard { Code = "D1-S6", SortOrder = 6, TextAr = "توفر المدرسة مصادر تعلم متنوعة تدعم تنفيذ المناهج." });
        version.Domains.Add(d1);

        // ── D2: التدريس والتعلم (4 standards) ──────────────────────────────
        var d2 = new RubricDomain { Code = "D2", NameAr = "التدريس والتعلم", SortOrder = 2 };
        d2.Standards.Add(new RubricStandard { Code = "D2-S1", SortOrder = 1, TextAr = "ينفذ المعلم أنشطة واستراتيجيات تدريس تستوفي نواتج التعلم." });
        d2.Standards.Add(new RubricStandard { Code = "D2-S2", SortOrder = 2, TextAr = "تتنوع استراتيجيات التدريس وفق قدرات المتعلمين وتراعي الفروق الفردية." });
        d2.Standards.Add(new RubricStandard { Code = "D2-S3", SortOrder = 3, TextAr = "يستخدم المعلم مصادر تعلم رقمية تلبي احتياجات المتعلمين." });
        d2.Standards.Add(new RubricStandard { Code = "D2-S4", SortOrder = 4, TextAr = "تنفذ المدرسة أنشطة تعليم وتعلم ترتبط بحياة المتعلمين." });
        version.Domains.Add(d2);

        // ── D3: تنمية المهارات (6 standards) ───────────────────────────────
        var d3 = new RubricDomain { Code = "D3", NameAr = "تنمية المهارات", SortOrder = 3 };
        d3.Standards.Add(new RubricStandard { Code = "D3-S1", SortOrder = 1, TextAr = "تشجع بيئة التعلم على تنمية مهارات القراءة والكتابة." });
        d3.Standards.Add(new RubricStandard { Code = "D3-S2", SortOrder = 2, TextAr = "تشجع بيئة التعلم على تنمية المهارات العددية." });
        d3.Standards.Add(new RubricStandard { Code = "D3-S3", SortOrder = 3, TextAr = "تشجع الممارسات التدريسية على تنمية مهارات التفكير والبحث والابتكار." });
        d3.Standards.Add(new RubricStandard { Code = "D3-S4", SortOrder = 4, TextAr = "تشجع بيئة التعلم تنمية المهارات العاطفية والاجتماعية." });
        d3.Standards.Add(new RubricStandard { Code = "D3-S5", SortOrder = 5, TextAr = "يستخدم المعلم أساليب تحفيز تعزز الدافعية لدى المتعلمين." });
        d3.Standards.Add(new RubricStandard { Code = "D3-S6", SortOrder = 6, TextAr = "يشارك المتعلمون في أنشطة التعلم بفاعلية ويستمتعون بها." });
        version.Domains.Add(d3);

        // ── D4: التقويم (3 standards) ───────────────────────────────────────
        var d4 = new RubricDomain { Code = "D4", NameAr = "التقويم", SortOrder = 4 };
        d4.Standards.Add(new RubricStandard { Code = "D4-S1", SortOrder = 1, TextAr = "يستخدم المعلم أساليب وأدوات تقويم متنوعة تشخيصية وبنائية وختامية." });
        d4.Standards.Add(new RubricStandard { Code = "D4-S2", SortOrder = 2, TextAr = "يطبق المعلم أساليب وأدوات تقويم متنوعة لقياس تحقق نواتج التعلم." });
        d4.Standards.Add(new RubricStandard { Code = "D4-S3", SortOrder = 3, TextAr = "يقدم المعلم تغذية راجعة متنوعة تركز على تحسين أداء المتعلمين." });
        version.Domains.Add(d4);

        // ── D5: سلوك المتعلمين (6 standards) ───────────────────────────────
        var d5 = new RubricDomain { Code = "D5", NameAr = "سلوك المتعلمين", SortOrder = 5 };
        d5.Standards.Add(new RubricStandard { Code = "D5-S1", SortOrder = 1, TextAr = "يظهر المتعلمون الاعتزاز بالقيم والهوية الوطنية." });
        d5.Standards.Add(new RubricStandard { Code = "D5-S2", SortOrder = 2, TextAr = "يظهر المتعلمون الاتجاهات الإيجابية نحو ذواتهم والآخرين." });
        d5.Standards.Add(new RubricStandard { Code = "D5-S3", SortOrder = 3, TextAr = "يظهر المتعلمون التزاماً بالممارسات الصحية السليمة." });
        d5.Standards.Add(new RubricStandard { Code = "D5-S4", SortOrder = 4, TextAr = "يلتزم المتعلمون بقواعد السلوك والانضباط." });
        d5.Standards.Add(new RubricStandard { Code = "D5-S5", SortOrder = 5, TextAr = "يظهر المتعلمون الاستقلالية والقدرة على التعلم الذاتي." });
        d5.Standards.Add(new RubricStandard { Code = "D5-S6", SortOrder = 6, TextAr = "يظهر المتعلمون الاعتزاز بثقافتهم واحترام التنوع الثقافي في المجتمع." });
        version.Domains.Add(d5);

        _context.RubricVersions.Add(version);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Rubric seeded: Version 1 active, {DomainCount} domains, {StandardCount} standards.",
            version.Domains.Count,
            version.Domains.Sum(d => d.Standards.Count));
    }
}
