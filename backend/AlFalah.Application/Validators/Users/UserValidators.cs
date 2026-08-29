using AlFalah.Application.DTOs.Users;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.Users;

public class UserCreateRequestValidator : AbstractValidator<UserCreateRequestDto>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب.")
            .MinimumLength(3).WithMessage("يجب ألا يقل اسم المستخدم عن 3 أحرف.")
            .MaximumLength(256).WithMessage("يجب ألا يتجاوز اسم المستخدم عن 256 حرفاً.");

        // Every account type — instructors included — supplies its own password.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(6).WithMessage("يجب ألا تقل كلمة المرور عن 6 خانات.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("الاسم الأول مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("اسم العائلة مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("البريد الإلكتروني غير صالح.");

        RuleFor(x => x.PreferredLanguage)
            .Must(l => l == "ar" || l == "en")
            .WithMessage("اللغة المفضلة يجب أن تكون 'ar' أو 'en'.");

        // School staff roles are created through this endpoint.
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب.")
            .Must(BeInPhaseTwoScope)
            .WithMessage("الدور يجب أن يكون أحد: SchoolManager أو Secretary أو Moderator أو Instructor.");

        RuleFor(x => x.SchoolId)
            .GreaterThan(0).When(x => x.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        // D-74 — Teacher-profile fields (Instructor only). When the role is Instructor
        // the service requires EmployeeNumber + Subject + SchoolId (from a rule above)
        // and the school-scope guard checks the school. For other roles these fields
        // are silently ignored.
        RuleFor(x => x.EmployeeNumber)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("الرقم الوظيفي مطلوب للمعلم.")
            .MaximumLength(50).When(r => !string.IsNullOrEmpty(r.EmployeeNumber))
            .WithMessage("يجب ألا يتجاوز الرقم الوظيفي 50 حرفاً.");

        RuleFor(x => x.Subject)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المادة الدراسية مطلوبة للمعلم.")
            .MaximumLength(200).When(r => !string.IsNullOrEmpty(r.Subject))
            .WithMessage("يجب ألا تتجاوز المادة الدراسية 200 حرف.");

        RuleFor(x => x.FullName)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("الاسم الكامل مطلوب للمعلم.")
            .MaximumLength(200).When(r => !string.IsNullOrEmpty(r.FullName))
            .WithMessage("يجب ألا يتجاوز الاسم الكامل 200 حرف.");

        // When role=Instructor the school is REQUIRED (we need to know where the
        // teacher teaches before we can store EmployeeNumber / Subject / Classes).
        RuleFor(x => x.SchoolId)
            .NotNull().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المدرسة مطلوبة للمعلم.")
            .GreaterThan(0).When(r => r.Role == RoleNames.Instructor && r.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        RuleFor(x => x.SchoolId)
            .NotNull().When(r => r.Role == RoleNames.Secretary)
            .WithMessage("المدرسة مطلوبة للسكرتير.")
            .GreaterThan(0).When(r => r.Role == RoleNames.Secretary && r.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        // Stage enum is optional (null is fine — the service falls back to the school's stage).

        RuleFor(x => x.Stage)
            .NotNull().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المرحلة الدراسية مطلوبة للمعلم.");

        // Class labels: max length each; trim; cap the count so a single
        // payload can't blow the dropdown.
        When(x => x.Classes != null, () =>
        {
            RuleForEach(x => x.Classes!)
                .NotEmpty().WithMessage("لا يمكن إضافة صف فارغ.")
                .MaximumLength(50).WithMessage("يجب ألا يتجاوز اسم الصف 50 حرفاً.");
            RuleFor(x => x.Classes!.Count)
                .LessThanOrEqualTo(50)
                .WithMessage("لا يمكن إضافة أكثر من 50 صفاً للمعلم.");
        });
    }

    private static bool BeInPhaseTwoScope(string role) =>
        role is RoleNames.SchoolManager or RoleNames.Secretary
             or RoleNames.Moderator     or RoleNames.Instructor;
}

public class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequestDto>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("الاسم الأول مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("اسم العائلة مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("البريد الإلكتروني غير صالح.");

        RuleFor(x => x.PreferredLanguage)
            .Must(l => l == "ar" || l == "en")
            .WithMessage("اللغة المفضلة يجب أن تكون 'ar' أو 'en'.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب.")
            .Must(r => r is RoleNames.SchoolManager or RoleNames.Secretary
                          or RoleNames.Moderator     or RoleNames.Instructor)
            .WithMessage("الدور يجب أن يكون أحد: SchoolManager أو Secretary أو Moderator أو Instructor.");

        RuleFor(x => x.SchoolId)
            .GreaterThan(0).When(x => x.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        // D-74 — Instructor updates can also edit teacher-profile fields.
        RuleFor(x => x.EmployeeNumber)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("الرقم الوظيفي مطلوب للمعلم.")
            .MaximumLength(50).When(r => !string.IsNullOrEmpty(r.EmployeeNumber))
            .WithMessage("يجب ألا يتجاوز الرقم الوظيفي 50 حرفاً.");

        RuleFor(x => x.Subject)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المادة الدراسية مطلوبة للمعلم.")
            .MaximumLength(200).When(r => !string.IsNullOrEmpty(r.Subject))
            .WithMessage("يجب ألا تتجاوز المادة الدراسية 200 حرف.");

        RuleFor(x => x.FullName)
            .NotEmpty().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("الاسم الكامل مطلوب للمعلم.")
            .MaximumLength(200).When(r => !string.IsNullOrEmpty(r.FullName))
            .WithMessage("يجب ألا يتجاوز الاسم الكامل 200 حرف.");

        RuleFor(x => x.SchoolId)
            .NotNull().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المدرسة مطلوبة للمعلم.")
            .GreaterThan(0).When(r => r.Role == RoleNames.Instructor && r.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        RuleFor(x => x.SchoolId)
            .NotNull().When(r => r.Role == RoleNames.Secretary)
            .WithMessage("المدرسة مطلوبة للسكرتير.")
            .GreaterThan(0).When(r => r.Role == RoleNames.Secretary && r.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");

        RuleFor(x => x.Stage)
            .NotNull().When(r => r.Role == RoleNames.Instructor)
            .WithMessage("المرحلة الدراسية مطلوبة للمعلم.");

        When(x => x.Classes != null, () =>
        {
            RuleForEach(x => x.Classes!)
                .NotEmpty().WithMessage("لا يمكن إضافة صف فارغ.")
                .MaximumLength(50).WithMessage("يجب ألا يتجاوز اسم الصف 50 حرفاً.");
            RuleFor(x => x.Classes!.Count)
                .LessThanOrEqualTo(50)
                .WithMessage("لا يمكن إضافة أكثر من 50 صفاً للمعلم.");
        });
    }
}
