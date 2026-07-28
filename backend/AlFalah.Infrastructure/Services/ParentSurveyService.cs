using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AlFalah.Application.DTOs.ParentSurveys;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Services;

public class ParentSurveyService : IParentSurveyService
{
    private static readonly Regex MobileRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);

    private readonly IParentSurveyRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;

    public ParentSurveyService(
        IParentSurveyRepository repository,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit)
    {
        _repository = repository;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ParentSurveyDto>> ListAsync(
        bool templates,
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        var allowedSchoolId = _scopeGuard.ResolveAllowedSchoolId(schoolId);
        var surveys = await _repository.ListAsync(allowedSchoolId, templates, cancellationToken);
        return surveys.Select(MapSurvey).ToList();
    }

    public async Task<ParentSurveyDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(id, false, cancellationToken);
        return MapSurvey(survey);
    }

    public async Task<ParentSurveyDto> CreateAsync(
        SaveParentSurveyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var schoolId = ResolveCreateSchoolId(request.SchoolId);
        await _scopeGuard.EnsureCanMutateSchoolAsync(schoolId, cancellationToken);

        if (!await _repository.SchoolExistsAsync(schoolId, cancellationToken))
            throw new KeyNotFoundException("المدرسة غير موجودة أو غير نشطة.");

        var itemTexts = await ResolveItemTextsAsync(request, schoolId, cancellationToken);
        ValidateDefinition(request.Title, request.Description, itemTexts);

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول لإنشاء النموذج.");

        var survey = new ParentSurvey
        {
            SchoolId = schoolId,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            IsTemplate = request.IsTemplate,
            Status = ParentSurveyStatus.Draft,
            CreatedByUserId = userId,
            Items = itemTexts.Select((text, index) => new ParentSurveyItem
            {
                Text = text,
                SortOrder = index + 1
            }).ToList()
        };

        await _repository.AddAsync(survey, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _audit.Write(
            schoolId,
            userId,
            request.IsTemplate ? "ParentSurveyTemplate.Create" : "ParentSurvey.Create",
            nameof(ParentSurvey),
            survey.Id.ToString(),
            request.IsTemplate ? "إنشاء قالب استبيان أولياء الأمور" : "إنشاء استبيان أولياء الأمور",
            newValues: new { survey.Title, ItemCount = survey.Items.Count });
        await _repository.SaveChangesAsync(cancellationToken);

        return await GetAsync(survey.Id, cancellationToken);
    }

    public async Task<ParentSurveyDto> UpdateAsync(
        int id,
        SaveParentSurveyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(id, true, cancellationToken);
        await _scopeGuard.EnsureCanMutateSchoolAsync(survey.SchoolId, cancellationToken);

        if (survey.IsTemplate != request.IsTemplate)
            throw new InvalidOperationException("لا يمكن تحويل النموذج إلى قالب أو العكس بعد إنشائه.");
        if (survey.Submissions.Count > 0)
            throw new InvalidOperationException("لا يمكن تغيير بنود نموذج استقبل ردودًا. أنشئ نموذجًا جديدًا من القالب للحفاظ على الردود السابقة.");

        var itemTexts = (request.Items ?? Array.Empty<ParentSurveyItemWriteDto>())
            .Select(x => x.Text.Trim())
            .ToList();
        ValidateDefinition(request.Title, request.Description, itemTexts);

        var oldTitle = survey.Title;
        survey.Title = request.Title.Trim();
        survey.Description = NormalizeOptional(request.Description);
        survey.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var item in survey.Items)
            item.IsDeleted = true;

        foreach (var item in itemTexts.Select((text, index) => new ParentSurveyItem
        {
            Text = text,
            SortOrder = index + 1
        }))
        {
            survey.Items.Add(item);
        }

        _audit.Write(
            survey.SchoolId,
            _currentUser.UserId,
            survey.IsTemplate ? "ParentSurveyTemplate.Update" : "ParentSurvey.Update",
            nameof(ParentSurvey),
            survey.Id.ToString(),
            survey.IsTemplate ? "تعديل قالب استبيان أولياء الأمور" : "تعديل استبيان أولياء الأمور",
            oldValues: new { Title = oldTitle },
            newValues: new { survey.Title, ItemCount = itemTexts.Count });

        await _repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(survey.Id, cancellationToken);
    }

    public async Task<PublishParentSurveyDto> PublishAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(id, true, cancellationToken);
        await _scopeGuard.EnsureCanMutateSchoolAsync(survey.SchoolId, cancellationToken);

        if (survey.IsTemplate)
            throw new InvalidOperationException("القالب لا يملك رابطًا عامًا. أنشئ نموذجًا منه أولًا.");
        if (survey.Status == ParentSurveyStatus.Closed)
            throw new InvalidOperationException("النموذج مغلق. أنشئ نسخة جديدة لإعادة الإرسال.");
        if (survey.Items.Count == 0)
            throw new InvalidOperationException("أضف بند تقييم واحدًا على الأقل قبل إنشاء الرابط.");

        if (string.IsNullOrWhiteSpace(survey.PublicToken))
            survey.PublicToken = await CreateUniqueTokenAsync(cancellationToken);

        survey.Status = ParentSurveyStatus.Published;
        survey.PublishedAt ??= DateTimeOffset.UtcNow;
        survey.UpdatedAt = DateTimeOffset.UtcNow;

        _audit.Write(
            survey.SchoolId,
            _currentUser.UserId,
            "ParentSurvey.Publish",
            nameof(ParentSurvey),
            survey.Id.ToString(),
            "نشر استبيان أولياء الأمور وإنشاء الرابط");
        await _repository.SaveChangesAsync(cancellationToken);

        return new PublishParentSurveyDto(survey.PublicToken, survey.PublishedAt.Value);
    }

    public async Task CloseAsync(int id, CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(id, true, cancellationToken);
        await _scopeGuard.EnsureCanMutateSchoolAsync(survey.SchoolId, cancellationToken);

        if (survey.IsTemplate)
            throw new InvalidOperationException("لا يمكن إغلاق قالب.");

        survey.Status = ParentSurveyStatus.Closed;
        survey.ClosedAt = DateTimeOffset.UtcNow;
        survey.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Write(
            survey.SchoolId,
            _currentUser.UserId,
            "ParentSurvey.Close",
            nameof(ParentSurvey),
            survey.Id.ToString(),
            "إغلاق استبيان أولياء الأمور");
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(id, true, cancellationToken);
        await _scopeGuard.EnsureCanMutateSchoolAsync(survey.SchoolId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        survey.IsDeleted = true;
        survey.DeletedAt = now;
        survey.DeletedByUserId = _currentUser.UserId;
        survey.UpdatedAt = now;
        foreach (var item in survey.Items)
            item.IsDeleted = true;

        _audit.Write(
            survey.SchoolId,
            _currentUser.UserId,
            survey.IsTemplate ? "ParentSurveyTemplate.Delete" : "ParentSurvey.Delete",
            nameof(ParentSurvey),
            survey.Id.ToString(),
            survey.IsTemplate ? "حذف قالب استبيان أولياء الأمور" : "حذف استبيان أولياء الأمور");
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicParentSurveyDto> GetPublicAsync(
        string publicToken,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetPublicSurveyAsync(publicToken, false, cancellationToken);
        return new PublicParentSurveyDto(
            survey.Title,
            survey.Description,
            survey.School.Name,
            survey.School.LogoUrl,
            survey.Status == ParentSurveyStatus.Published,
            survey.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new ParentSurveyItemDto(x.Id, x.Text, x.SortOrder))
                .ToList());
    }

    public async Task SubmitAsync(
        string publicToken,
        SubmitParentSurveyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetPublicSurveyAsync(publicToken, false, cancellationToken);
        if (survey.Status != ParentSurveyStatus.Published)
            throw new InvalidOperationException("هذا النموذج مغلق ولا يستقبل ردودًا جديدة.");

        var parentName = request.ParentName?.Trim() ?? string.Empty;
        var mobile = NormalizeMobile(request.MobileNumber);
        if (parentName.Length is < 2 or > 150)
            throw new ArgumentException("اسم ولي الأمر مطلوب ويجب ألا يزيد عن 150 حرفًا.");
        if (!MobileRegex.IsMatch(mobile))
            throw new ArgumentException("رقم الجوال غير صالح. استخدم من 8 إلى 15 رقمًا ويمكن إضافة + في البداية.");

        var answers = request.Answers ?? Array.Empty<SubmitParentSurveyAnswerDto>();
        var activeItems = survey.Items.OrderBy(x => x.SortOrder).ToList();
        if (answers.Count != activeItems.Count)
            throw new ArgumentException("يجب تقييم جميع البنود قبل إرسال النموذج.");

        var answerByItemId = answers
            .GroupBy(x => x.ItemId)
            .ToDictionary(x => x.Key, x => x.ToList());
        if (answerByItemId.Count != activeItems.Count
            || activeItems.Any(item => !answerByItemId.TryGetValue(item.Id, out var values) || values.Count != 1))
            throw new ArgumentException("الردود لا تطابق بنود النموذج.");

        var submission = new ParentSurveySubmission
        {
            ParentSurveyId = survey.Id,
            ParentName = parentName,
            MobileNumber = mobile
        };

        foreach (var item in activeItems)
        {
            var input = answerByItemId[item.Id][0];
            if (!Enum.IsDefined(input.Rating))
                throw new ArgumentException($"قيمة التقييم للبند «{item.Text}» غير صالحة.");

            var reason = NormalizeOptional(input.WeakReason);
            var shouldAdjust = input.Rating == ParentSurveyRating.Weak && reason is null;
            submission.Answers.Add(new ParentSurveyAnswer
            {
                ParentSurveyItemId = item.Id,
                ItemTextSnapshot = item.Text,
                SubmittedRating = input.Rating,
                EffectiveRating = shouldAdjust ? ParentSurveyRating.VeryGood : input.Rating,
                WeakReason = input.Rating == ParentSurveyRating.Weak ? reason : null,
                WasAutoAdjusted = shouldAdjust
            });
        }

        await _repository.AddSubmissionAsync(submission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParentSurveySubmissionListItemDto>> ListSubmissionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(surveyId, false, cancellationToken);
        if (survey.IsTemplate)
            throw new InvalidOperationException("القوالب لا تحتوي على ردود.");

        var submissions = await _repository.ListSubmissionsAsync(surveyId, cancellationToken);
        return submissions.Select(x => new ParentSurveySubmissionListItemDto(
            x.Id,
            x.ParentName,
            x.MobileNumber,
            x.SubmittedAt,
            x.Answers.Count(a => a.WasAutoAdjusted))).ToList();
    }

    public async Task<ParentSurveySubmissionDto> GetSubmissionAsync(
        int surveyId,
        int submissionId,
        CancellationToken cancellationToken = default)
    {
        var survey = await GetAccessibleAsync(surveyId, false, cancellationToken);
        if (survey.IsTemplate)
            throw new InvalidOperationException("القوالب لا تحتوي على ردود.");

        var submission = await _repository.GetSubmissionAsync(surveyId, submissionId, cancellationToken)
            ?? throw new KeyNotFoundException("الرد غير موجود.");

        return new ParentSurveySubmissionDto(
            submission.Id,
            submission.ParentSurveyId,
            submission.ParentName,
            submission.MobileNumber,
            submission.SubmittedAt,
            submission.Answers.Select(x => new ParentSurveyAnswerDto(
                x.ParentSurveyItemId,
                x.ItemTextSnapshot,
                x.SubmittedRating,
                x.EffectiveRating,
                x.WeakReason,
                x.WasAutoAdjusted)).ToList());
    }

    private async Task<ParentSurvey> GetAccessibleAsync(
        int id,
        bool track,
        CancellationToken cancellationToken)
    {
        var survey = await _repository.GetAsync(id, track, cancellationToken)
            ?? throw new KeyNotFoundException("النموذج غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(survey.SchoolId, cancellationToken);
        return survey;
    }

    private async Task<ParentSurvey> GetPublicSurveyAsync(
        string publicToken,
        bool track,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicToken) || publicToken.Length > 64)
            throw new KeyNotFoundException("رابط النموذج غير صالح.");

        return await _repository.GetPublicAsync(publicToken, track, cancellationToken)
            ?? throw new KeyNotFoundException("رابط النموذج غير صالح أو لم يعد متاحًا.");
    }

    private int ResolveCreateSchoolId(int? requestedSchoolId)
    {
        var resolved = _scopeGuard.ResolveAllowedSchoolId(requestedSchoolId);
        return resolved
            ?? throw new ArgumentException("يجب اختيار المدرسة التي سيتبع لها النموذج.");
    }

    private async Task<IReadOnlyList<string>> ResolveItemTextsAsync(
        SaveParentSurveyRequestDto request,
        int schoolId,
        CancellationToken cancellationToken)
    {
        var provided = (request.Items ?? Array.Empty<ParentSurveyItemWriteDto>())
            .Select(x => x.Text.Trim())
            .Where(x => x.Length > 0)
            .ToList();
        if (provided.Count > 0 || request.SourceTemplateId is null)
            return provided;

        var template = await GetAccessibleAsync(request.SourceTemplateId.Value, false, cancellationToken);
        if (!template.IsTemplate)
            throw new InvalidOperationException("المصدر المختار ليس قالبًا.");
        if (template.SchoolId != schoolId)
            throw new InvalidOperationException("لا يمكن استخدام قالب تابع لمدرسة أخرى.");
        return template.Items.OrderBy(x => x.SortOrder).Select(x => x.Text).ToList();
    }

    private static void ValidateDefinition(string title, string? description, IReadOnlyList<string> itemTexts)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length is < 2 or > 200)
            throw new ArgumentException("عنوان النموذج مطلوب ويجب ألا يزيد عن 200 حرف.");
        if ((description?.Trim().Length ?? 0) > 2000)
            throw new ArgumentException("وصف النموذج يجب ألا يزيد عن 2000 حرف.");
        if (itemTexts.Count is < 1 or > 100)
            throw new ArgumentException("يجب أن يحتوي النموذج على بند واحد على الأقل وبحد أقصى 100 بند.");
        if (itemTexts.Any(x => x.Length is < 2 or > 500))
            throw new ArgumentException("كل بند تقييم مطلوب ويجب ألا يزيد عن 500 حرف.");
        if (itemTexts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != itemTexts.Count)
            throw new ArgumentException("لا يمكن تكرار نفس بند التقييم داخل النموذج.");
    }

    private async Task<string> CreateUniqueTokenAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            if (!await _repository.PublicTokenExistsAsync(token, cancellationToken))
                return token;
        }
    }

    private static string NormalizeMobile(string? value) =>
        Regex.Replace(value?.Trim() ?? string.Empty, @"[\s()-]", string.Empty);

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static ParentSurveyDto MapSurvey(ParentSurvey survey) =>
        new(
            survey.Id,
            survey.SchoolId,
            survey.School.Name,
            survey.Title,
            survey.Description,
            survey.IsTemplate,
            survey.Status,
            survey.PublicToken,
            survey.Submissions.Count,
            survey.CreatedAt,
            survey.UpdatedAt,
            survey.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new ParentSurveyItemDto(x.Id, x.Text, x.SortOrder))
                .ToList());
}
