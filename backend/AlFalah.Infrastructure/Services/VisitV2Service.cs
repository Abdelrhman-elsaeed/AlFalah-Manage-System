using System.IO.Compression;
using System.Text.Json;
using AlFalah.Application.Analysis;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class VisitV2Service(
    IVisitV2Repository repository,
    IVisitV2DocumentService documents,
    ICurrentUserService currentUser,
    IFeatureFlagService featureFlags,
    SchoolScopeGuard schoolScope,
    AuditLogWriter audit,
    ILogger<VisitV2Service> logger) : IVisitV2Service
{
    public VisitV2AvailabilityDto GetAvailability() =>
        new(featureFlags.IsVisitsV2Enabled(currentUser.ActiveSchoolId));

    public async Task<VisitV2ObservationCardDto> GetObservationCardAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var rubric = await GetRubricAsync(cancellationToken);
        return new VisitV2ObservationCardDto(
            rubric.Id,
            rubric.VersionNumber,
            rubric.Domains.OrderBy(d => d.SortOrder).Select(d => new VisitV2DomainDto(
                d.Id, d.Code, d.NameAr, d.SortOrder,
                d.Standards.OrderBy(s => s.SortOrder).Select(s => new VisitV2StandardDto(
                    s.Id, s.Code, s.TextAr, s.SortOrder, 1, null,
                    s.Indicators.OrderBy(i => i.SortOrder)
                        .Select(i => new VisitV2IndicatorDto(i.Id, i.Code, i.TextAr, i.SortOrder, false)).ToList()
                )).ToList())).ToList(),
            ScoreLabels);
    }

    public async Task<VisitV2DetailDto> CreateAsync(
        CreateVisitV2RequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var schoolId = RequireActiveSchool();
        await schoolScope.EnsureCanMutateSchoolAsync(schoolId, cancellationToken);
        ValidateMetadata(request.ClassroomPeriod, request.PresentCount, request.AbsentCount);
        ValidateEnums(request.VisitCategory, request.VisitSequence);

        if (!await repository.IsActiveInstructorAsync(request.InstructorId, schoolId, cancellationToken))
            throw new ArgumentException("المعلم المحدد غير نشط أو غير تابع للمدرسة الحالية.");

        var evaluatorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مسجل الدخول.");
        var evaluator = await repository.GetUserAsync(evaluatorId, cancellationToken)
            ?? throw new UnauthorizedAccessException("حساب المقيم غير موجود أو غير نشط.");
        var rubric = await GetRubricAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var visit = new Visit
        {
            SchoolId = schoolId,
            InstructorId = request.InstructorId,
            CreatedByUserId = evaluatorId,
            RubricVersionId = rubric.Id,
            VisitCategory = (VisitCategory)request.VisitCategory,
            VisitSequence = (VisitSequence)request.VisitSequence,
            Status = VisitStatus.Draft,
            VisitDate = request.VisitDate,
            ClassroomPeriod = request.ClassroomPeriod,
            Subject = request.Subject.Trim(),
            GradeClass = request.GradeClass.Trim(),
            LessonTitle = request.LessonTitle.Trim(),
            PresentCount = request.PresentCount,
            AbsentCount = request.AbsentCount,
            Notes = NullIfWhiteSpace(request.Notes),
            ExperienceVersion = ExperienceVersion.PrototypeV2,
            ScoringRuleSetVersion = 2,
            EvaluatorNameSnapshot = evaluator.FullName,
            EvaluatorRoleSnapshot = await repository.GetEvaluatorRoleAsync(evaluatorId, schoolId, cancellationToken),
            CreatedAt = now,
            UpdatedAt = now,
            Scores = rubric.Domains.SelectMany(d => d.Standards).Select(s => new VisitScore
            {
                RubricStandardId = s.Id,
                Score = 1,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList()
        };

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await repository.AddAsync(visit, ct);
            await repository.SaveChangesAsync(ct);
            audit.Write(schoolId, evaluatorId, "VisitV2.Create", nameof(Visit), visit.Id.ToString(), null,
                newValues: new { visit.InstructorId, visit.RubricVersionId, visit.ExperienceVersion });
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(visit.Id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> UpdateAsync(
        int id,
        UpdateVisitV2RequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateMetadata(request.ClassroomPeriod, request.PresentCount, request.AbsentCount);
        ValidateEnums(request.VisitCategory, request.VisitSequence);
        var visit = await GetAuthorizedVisitAsync(id, true, true, cancellationToken);
        EnsureEditable(visit);

        var expectedIds = visit.Scores.Select(s => s.RubricStandardId).OrderBy(x => x).ToArray();
        var submittedIds = request.Scores.Select(s => s.RubricStandardId).OrderBy(x => x).ToArray();
        if (request.Scores.Count != submittedIds.Distinct().Count() || !expectedIds.SequenceEqual(submittedIds))
            throw new ArgumentException("يجب إرسال درجة واحدة لكل معيار من نسخة أداة الزيارة دون تكرار.");
        if (request.Scores.Any(s => s.Score is < 1 or > 4))
            throw new ArgumentException("درجات الزيارة V2 يجب أن تكون بين 1 و4.");

        var indicatorsByStandard = await repository.GetIndicatorsByStandardAsync(
            visit.RubricVersionId, expectedIds, cancellationToken);

        foreach (var input in request.Scores)
        {
            if (input.ObservedIndicatorIds.Count != input.ObservedIndicatorIds.Distinct().Count())
                throw new ArgumentException("لا يمكن تكرار مؤشر الرصد داخل المعيار نفسه.");
            var allowed = indicatorsByStandard.GetValueOrDefault(input.RubricStandardId, new List<RubricIndicator>());
            var allowedIds = allowed.Select(i => i.Id).ToHashSet();
            if (input.ObservedIndicatorIds.Any(idValue => !allowedIds.Contains(idValue)))
                throw new ArgumentException("أحد مؤشرات الرصد لا ينتمي إلى المعيار أو نسخة الأداة المحددة.");
        }

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            visit.VisitCategory = (VisitCategory)request.VisitCategory;
            visit.VisitSequence = (VisitSequence)request.VisitSequence;
            visit.VisitDate = request.VisitDate;
            visit.ClassroomPeriod = request.ClassroomPeriod;
            visit.Subject = request.Subject.Trim();
            visit.GradeClass = request.GradeClass.Trim();
            visit.LessonTitle = request.LessonTitle.Trim();
            visit.PresentCount = request.PresentCount;
            visit.AbsentCount = request.AbsentCount;
            visit.Notes = NullIfWhiteSpace(request.Notes);
            visit.UpdatedAt = DateTimeOffset.UtcNow;

            foreach (var input in request.Scores)
            {
                var score = visit.Scores.Single(s => s.RubricStandardId == input.RubricStandardId);
                score.Score = input.Score;
                score.EvidenceNote = NullIfWhiteSpace(input.EvidenceNote);
                score.UpdatedAt = DateTimeOffset.UtcNow;
                var selected = input.ObservedIndicatorIds.ToHashSet();
                foreach (var existing in score.ObservedIndicators)
                {
                    existing.IsDeleted = !selected.Contains(existing.RubricIndicatorId);
                    existing.DeletedAt = existing.IsDeleted ? DateTimeOffset.UtcNow : null;
                    existing.DeletedByUserId = existing.IsDeleted ? currentUser.UserId : null;
                }
                foreach (var indicator in indicatorsByStandard[input.RubricStandardId]
                             .Where(i => selected.Contains(i.Id) && score.ObservedIndicators.All(o => o.RubricIndicatorId != i.Id)))
                {
                    score.ObservedIndicators.Add(new VisitObservedIndicator
                    {
                        RubricIndicatorId = indicator.Id,
                        IndicatorTextArSnapshot = indicator.TextAr,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            audit.Write(visit.SchoolId, currentUser.UserId, "VisitV2.Update", nameof(Visit), visit.Id.ToString(), null,
                newValues: new { request.ClassroomPeriod, ScoreCount = request.Scores.Count });
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> FinalizeAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var visit = await GetAuthorizedVisitAsync(id, true, true, cancellationToken);
        EnsureEditable(visit);
        if (visit.Scores.Count == 0 || visit.Scores.Any(s => s.Score is null or < 1 or > 4))
            throw new BusinessRuleException("يجب رصد جميع معايير الزيارة بدرجات من 1 إلى 4 قبل الإنهاء.");

        var inputs = visit.Scores.Select(s => new StandardScoreV2Input
        {
            DomainId = s.RubricStandard.Domain.Id,
            DomainName = s.RubricStandard.Domain.NameAr,
            StandardId = s.RubricStandardId,
            StandardText = s.RubricStandard.TextAr,
            Score = s.Score!.Value
        }).ToList();
        var result = VisitV2AnalysisEngine.Calculate(inputs);
        var now = DateTimeOffset.UtcNow;
        var autoApprove = IsManager() && currentUser.HasPermission(PermissionNames.VisitApprove);
        var actorId = RequireCurrentUser();

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var analysis = visit.Analysis ?? new VisitAnalysis { VisitId = visit.Id };
            analysis.OverallScore = Math.Round(result.OverallPercent / 25m, 3);
            analysis.TotalScore = result.TotalScore;
            analysis.MaximumScore = result.MaxTotalScore;
            analysis.OverallPercentage = result.OverallPercent;
            analysis.RuleSetVersion = 2;
            analysis.PerformanceLevelAr = result.OverallLevel;
            analysis.StrengthsJson = JsonSerializer.Serialize(result.Strengths);
            analysis.ImprovementAreasJson = JsonSerializer.Serialize(result.Weaknesses.Select(w => w.DomainName));
            analysis.PriorityStandardsJson = "[]";
            analysis.ComputedAt = now;
            analysis.IsDeleted = false;
            analysis.DeletedAt = null;
            analysis.DeletedByUserId = null;

            foreach (var domain in result.DomainScores)
            {
                var row = analysis.DomainAverages.FirstOrDefault(x => x.RubricDomainId == domain.DomainId);
                if (row == null)
                {
                    row = new VisitDomainAverage { RubricDomainId = domain.DomainId };
                    analysis.DomainAverages.Add(row);
                }
                row.DomainCode = visit.Scores.First(s => s.RubricStandard.Domain.Id == domain.DomainId).RubricStandard.Domain.Code;
                row.DomainNameAr = domain.DomainName;
                row.AverageScore = Math.Round(domain.Sum / (decimal)domain.StandardsCount, 3);
                row.PercentageScore = domain.Percent;
                row.IsDeleted = false;
            }
            visit.Analysis = analysis;

            if (visit.TreatmentSnapshots.Count == 0)
            {
                var order = 1;
                foreach (var weakness in result.Weaknesses)
                {
                    if (!RubricV2Seeder.TreatmentTemplates.TryGetValue(weakness.DomainName, out var template)) continue;
                    visit.TreatmentSnapshots.Add(new VisitTreatmentSnapshot
                    {
                        RubricDomainId = weakness.DomainId,
                        DomainNameArSnapshot = weakness.DomainName,
                        Goal = template.Goal,
                        Actions = template.Actions,
                        SuccessIndicators = template.SuccessIndicators,
                        Source = TreatmentSource.Generated,
                        SortOrder = order++,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            visit.Status = autoApprove ? VisitStatus.Approved : VisitStatus.PendingApproval;
            visit.SubmittedAt = now;
            visit.ApprovedByUserId = autoApprove ? actorId : null;
            visit.ApprovedAt = autoApprove ? now : null;
            visit.RejectionReason = null;
            visit.ReopenReason = null;
            visit.UpdatedAt = now;
            audit.Write(visit.SchoolId, actorId, "VisitV2.Finalize", nameof(Visit), visit.Id.ToString(), null,
                newValues: new { result.TotalScore, result.OverallPercent, visit.Status, AutoApproved = autoApprove });
            if (autoApprove)
            {
                audit.Write(visit.SchoolId, actorId, "VisitV2.AutoApprove", nameof(Visit), visit.Id.ToString(),
                    "اعتماد تلقائي لأن مُنهي الزيارة مدير مخول بالاعتماد.",
                    newValues: new { visit.Status, visit.ApprovedByUserId, visit.ApprovedAt });
            }
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        EnsureWorkflowPermission(PermissionNames.VisitApprove, "هذا الإجراء متاح فقط لمدير المدرسة المعني بالزيارة.");
        var visit = await GetWorkflowVisitAsync(id, VisitStatus.PendingApproval, "اعتماد", cancellationToken);
        var userId = RequireCurrentUser();
        var now = DateTimeOffset.UtcNow;
        var oldStatus = visit.Status;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            visit.Status = VisitStatus.Approved;
            visit.ApprovedByUserId = userId;
            visit.ApprovedAt = now;
            visit.RejectionReason = null;
            visit.ReopenReason = null;
            visit.ReopenedByUserId = null;
            visit.ReopenedAt = null;
            visit.UpdatedAt = now;
            audit.Write(visit.SchoolId, userId, "VisitV2.Approve", nameof(Visit), visit.Id.ToString(), "اعتماد الزيارة",
                new { Status = oldStatus }, new { visit.Status, visit.ApprovedByUserId, visit.ApprovedAt });
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> RejectAsync(
        int id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        EnsureWorkflowPermission(PermissionNames.VisitApprove, "هذا الإجراء متاح فقط لمدير المدرسة المعني بالزيارة.");
        reason = RequireReason(reason, "سبب الرفض مطلوب.");
        var visit = await GetWorkflowVisitAsync(id, VisitStatus.PendingApproval, "رفض", cancellationToken);
        var userId = RequireCurrentUser();
        var now = DateTimeOffset.UtcNow;
        var oldStatus = visit.Status;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            visit.Status = VisitStatus.RejectedForChanges;
            visit.RejectionReason = reason;
            visit.ApprovedByUserId = null;
            visit.ApprovedAt = null;
            visit.ReopenReason = null;
            visit.ReopenedByUserId = null;
            visit.ReopenedAt = null;
            visit.UpdatedAt = now;
            audit.Write(visit.SchoolId, userId, "VisitV2.Reject", nameof(Visit), visit.Id.ToString(), reason,
                new { Status = oldStatus }, new { visit.Status, visit.RejectionReason });
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> ReopenAsync(
        int id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        EnsureWorkflowPermission(PermissionNames.VisitReopen, "هذا الإجراء متاح فقط لمدير المدرسة المعني بالزيارة.");
        reason = RequireReason(reason, "سبب إعادة الفتح مطلوب.");
        var visit = await GetWorkflowVisitAsync(id, VisitStatus.Approved, "إعادة فتح", cancellationToken);
        var userId = RequireCurrentUser();
        var now = DateTimeOffset.UtcNow;
        var oldStatus = visit.Status;

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            visit.Status = VisitStatus.Reopened;
            visit.ReopenReason = reason;
            visit.ReopenedByUserId = userId;
            visit.ReopenedAt = now;
            visit.RejectionReason = null;
            visit.UpdatedAt = now;
            audit.Write(visit.SchoolId, userId, "VisitV2.Reopen", nameof(Visit), visit.Id.ToString(), reason,
                new { Status = oldStatus }, new { visit.Status, visit.ReopenReason, visit.ReopenedByUserId, visit.ReopenedAt });
            await repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<VisitV2DetailDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var visit = await GetAuthorizedVisitAsync(id, false, false, cancellationToken);
        if (IsInstructorOnly())
            await repository.AddReportViewAsync(visit.Id, currentUser.UserId!, cancellationToken);
        return MapDetail(visit);
    }

    public async Task<VisitV2ArchiveResultDto> ListAsync(
        VisitV2ArchiveQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ResolveReadScope(out var schoolId, out var creatorId, out var instructorId, out var approvedOnly);
        var page = await repository.ListAsync(query, schoolId, creatorId, instructorId, approvedOnly, cancellationToken);
        var evaluators = await repository.ListEvaluatorsAsync(schoolId, creatorId, cancellationToken);
        return new VisitV2ArchiveResultDto(page, evaluators);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var visit = await GetAuthorizedVisitAsync(id, true, true, cancellationToken);
        if (visit.Status == VisitStatus.Approved && !IsManager())
            throw new BusinessRuleException("لا يمكن للمشرف حذف زيارة معتمدة.");
        var now = DateTimeOffset.UtcNow;
        visit.IsDeleted = true;
        visit.DeletedAt = now;
        visit.DeletedByUserId = currentUser.UserId;
        foreach (var score in visit.Scores)
        {
            score.IsDeleted = true;
            score.DeletedAt = now;
            score.DeletedByUserId = currentUser.UserId;
            foreach (var observation in score.ObservedIndicators)
            {
                observation.IsDeleted = true;
                observation.DeletedAt = now;
                observation.DeletedByUserId = currentUser.UserId;
            }
        }
        if (visit.Analysis != null)
        {
            visit.Analysis.IsDeleted = true;
            visit.Analysis.DeletedAt = now;
            visit.Analysis.DeletedByUserId = currentUser.UserId;
            foreach (var domain in visit.Analysis.DomainAverages)
            {
                domain.IsDeleted = true;
                domain.DeletedAt = now;
                domain.DeletedByUserId = currentUser.UserId;
            }
        }
        foreach (var treatment in visit.TreatmentSnapshots)
        {
            treatment.IsDeleted = true;
            treatment.DeletedAt = now;
            treatment.DeletedByUserId = currentUser.UserId;
        }
        audit.Write(visit.SchoolId, currentUser.UserId, "VisitV2.SoftDelete", nameof(Visit), id.ToString(), null);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VisitV2TreatmentDto>> UpdateTreatmentsAsync(
        int id,
        UpdateVisitV2TreatmentsDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var visit = await GetAuthorizedVisitAsync(id, true, true, cancellationToken);
        if (visit.Status is VisitStatus.Approved && !IsManager())
            throw new BusinessRuleException("يجب إعادة فتح الزيارة المعتمدة قبل تعديل الخطة العلاجية.");
        if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.Goal) || string.IsNullOrWhiteSpace(i.Actions) ||
                                   string.IsNullOrWhiteSpace(i.SuccessIndicators)))
            throw new ArgumentException("الهدف والإجراءات ومؤشرات النجاح مطلوبة لكل عنصر علاجي.");
        var allowedDomains = visit.Scores.Select(s => s.RubricStandard.Domain.Id).ToHashSet();
        if (request.Items.Any(i => i.RubricDomainId.HasValue && !allowedDomains.Contains(i.RubricDomainId.Value)))
            throw new ArgumentException("أحد المجالات العلاجية لا ينتمي إلى نسخة أداة الزيارة.");
        if (request.Items.Where(i => i.Id.HasValue).Select(i => i.Id).Distinct().Count() != request.Items.Count(i => i.Id.HasValue))
            throw new ArgumentException("لا يمكن تكرار عنصر الخطة العلاجية.");

        var now = DateTimeOffset.UtcNow;
        var requestedIds = request.Items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
        foreach (var existing in visit.TreatmentSnapshots.Where(t => !requestedIds.Contains(t.Id)))
        {
            existing.IsDeleted = true;
            existing.DeletedAt = now;
            existing.DeletedByUserId = currentUser.UserId;
        }
        foreach (var input in request.Items)
        {
            var row = input.Id.HasValue
                ? visit.TreatmentSnapshots.SingleOrDefault(t => t.Id == input.Id.Value)
                    ?? throw new ArgumentException("عنصر الخطة العلاجية غير موجود ضمن الزيارة.")
                : new VisitTreatmentSnapshot
                {
                    VisitId = visit.Id,
                    Source = TreatmentSource.Manual,
                    CreatedAt = now
                };
            row.RubricDomainId = input.RubricDomainId;
            row.DomainNameArSnapshot = input.DomainNameAr.Trim();
            row.Goal = input.Goal.Trim();
            row.Actions = input.Actions.Trim();
            row.SuccessIndicators = input.SuccessIndicators.Trim();
            row.SortOrder = input.SortOrder;
            row.UpdatedAt = now;
            row.IsDeleted = false;
            row.DeletedAt = null;
            row.DeletedByUserId = null;
            if (!input.Id.HasValue) visit.TreatmentSnapshots.Add(row);
        }
        audit.Write(visit.SchoolId, currentUser.UserId, "VisitV2.UpdateTreatments", nameof(Visit), id.ToString(), null,
            newValues: new { Count = request.Items.Count });
        await repository.SaveChangesAsync(cancellationToken);
        return (await GetAsync(id, cancellationToken)).Treatments;
    }

    public async Task<VisitV2DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ResolveReadScope(out var schoolId, out var creatorId, out _, out _);
        var teachers = await repository.CountActiveInstructorsAsync(schoolId, cancellationToken);
        return await repository.GetDashboardAsync(schoolId, creatorId, teachers, cancellationToken);
    }

    public async Task<VisitV2CsvExportDto> ExportCsvAsync(
        VisitV2ArchiveQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ResolveReadScope(out var schoolId, out var creatorId, out var instructorId, out var approvedOnly);
        var visits = await repository.ListForExportAsync(
            query, schoolId, creatorId, instructorId, approvedOnly, cancellationToken);
        var employees = await repository.GetEmployeeNumbersAsync(visits.Select(v => v.InstructorId).Distinct().ToArray(), cancellationToken);
        var rows = visits.Select(v =>
        {
            var percentages = (IReadOnlyDictionary<string, int>)(v.Analysis?.DomainAverages
                .Where(d => d.PercentageScore.HasValue)
                .ToDictionary(d => d.DomainNameAr, d => d.PercentageScore!.Value)
                ?? new Dictionary<string, int>());
            var strengths = v.Analysis == null ? Array.Empty<string>() :
                JsonSerializer.Deserialize<string[]>(v.Analysis.StrengthsJson) ?? Array.Empty<string>();
            var improvements = v.Analysis == null ? Array.Empty<string>() :
                JsonSerializer.Deserialize<string[]>(v.Analysis.ImprovementAreasJson) ?? Array.Empty<string>();
            return new VisitV2CsvRow(
                v.Id, v.VisitDate, v.ClassroomPeriod ?? 0, v.Instructor.FullName,
                employees.GetValueOrDefault(v.InstructorId) ?? string.Empty,
                v.Subject ?? string.Empty, v.GradeClass ?? string.Empty, v.LessonTitle ?? string.Empty,
                $"{v.VisitCategory.ToArabicString()} - {v.VisitSequence.ToArabicString()}",
                v.PresentCount, v.AbsentCount, v.EvaluatorNameSnapshot ?? v.CreatedByUser.FullName,
                v.EvaluatorRoleSnapshot ?? string.Empty, (int)(v.Analysis?.TotalScore ?? 0),
                v.Analysis?.PerformanceLevelAr ?? string.Empty, percentages,
                string.Join("، ", strengths), string.Join("، ", improvements));
        }).ToList();
        return documents.BuildCsv(rows);
    }

    public async Task<BulkExportResult> ExportZipAsync(
        VisitV2ArchiveQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ResolveReadScope(out var schoolId, out var creatorId, out var instructorId, out var approvedOnly);
        var visits = await repository.ListForExportAsync(
            query, schoolId, creatorId, instructorId, approvedOnly, cancellationToken);
        var assets = await repository.GetPdfAssetSourcesAsync(
            visits.Select(v => v.Id).ToArray(), cancellationToken);

        var renderedCount = 0;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var visit in visits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!assets.TryGetValue(visit.Id, out var visitAssets))
                        continue;
                    var pdf = await documents.BuildPdfAsync(MapDetail(visit), visitAssets, cancellationToken);
                    var entry = archive.CreateEntry($"visit-v2-{visit.Id}.pdf", CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdf.Content, cancellationToken);
                    renderedCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "V2 ZIP export skipped visit {VisitId}.", visit.Id);
                }
            }
        }

        logger.LogInformation(
            "V2 ZIP export completed: matched={MatchedCount} rendered={RenderedCount} user={UserId}",
            visits.Count, renderedCount, currentUser.UserId);
        return new BulkExportResult
        {
            ZipBytes = stream.ToArray(),
            FileName = $"visits-v2-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip",
            VisitCount = renderedCount
        };
    }

    public async Task<VisitV2PdfExportDto> ExportPdfAsync(int id, CancellationToken cancellationToken = default)
    {
        var visit = await GetAsync(id, cancellationToken);
        var assets = await repository.GetPdfAssetSourcesAsync(id, cancellationToken);
        return await documents.BuildPdfAsync(visit, assets, cancellationToken);
    }

    private async Task<RubricVersion> GetRubricAsync(CancellationToken cancellationToken) =>
        await repository.GetRubricAsync(cancellationToken)
        ?? throw new BusinessRuleException("لم يتم تهيئة أداة الزيارات V2 بعد.");

    private async Task<Visit> GetAuthorizedVisitAsync(
        int id,
        bool tracking,
        bool mutation,
        CancellationToken cancellationToken)
    {
        var visit = await repository.GetAsync(id, tracking, cancellationToken)
            ?? throw new KeyNotFoundException("الزيارة V2 غير موجودة.");
        if (mutation)
            await schoolScope.EnsureCanMutateSchoolAsync(visit.SchoolId, cancellationToken);
        else
        {
            var allowed = schoolScope.ResolveAllowedSchoolId(visit.SchoolId);
            if (allowed.HasValue && allowed.Value != visit.SchoolId)
                throw new UnauthorizedSchoolAccessException("لا تملك صلاحية الوصول إلى هذه الزيارة.");
        }
        if (IsModeratorOnly() && visit.CreatedByUserId != currentUser.UserId)
            throw new UnauthorizedSchoolAccessException("لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك.");
        if (IsInstructorOnly())
        {
            if (visit.InstructorId != currentUser.UserId || visit.Status != VisitStatus.Approved)
                throw new UnauthorizedSchoolAccessException("لا تملك صلاحية الوصول إلى هذا التقرير.");
        }
        return visit;
    }

    private async Task<Visit> GetWorkflowVisitAsync(
        int id,
        VisitStatus requiredStatus,
        string actionName,
        CancellationToken cancellationToken)
    {
        var visit = await GetAuthorizedVisitAsync(id, true, true, cancellationToken);
        if (visit.ExperienceVersion != ExperienceVersion.PrototypeV2)
            throw new KeyNotFoundException("الزيارة V2 غير موجودة.");
        if (visit.Status != requiredStatus)
            throw new BusinessRuleException($"لا يمكن {actionName} الزيارة في حالتها الحالية.");
        return visit;
    }

    private void EnsureWorkflowPermission(string permission, string message)
    {
        if (!currentUser.HasPermission(permission) || (!currentUser.IsGlobalAdmin() && !currentUser.IsInRole(RoleNames.SchoolManager)))
            throw new UnauthorizedSchoolAccessException(message);
    }

    private string RequireCurrentUser() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("المستخدم غير مسجل الدخول.");

    private static string RequireReason(string reason, string message)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException(message);
        return reason.Trim();
    }

    private void ResolveReadScope(
        out int? schoolId,
        out string? creatorId,
        out string? instructorId,
        out bool approvedOnly)
    {
        schoolId = currentUser.IsGlobalAdmin() ? null : RequireActiveSchool();
        creatorId = IsModeratorOnly() ? currentUser.UserId : null;
        instructorId = IsInstructorOnly() ? currentUser.UserId : null;
        approvedOnly = instructorId != null;
    }

    private int RequireActiveSchool() => currentUser.ActiveSchoolId
        ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة مرتبطة بالحساب.");

    private void EnsureEnabled()
    {
        if (!featureFlags.IsVisitsV2Enabled(currentUser.ActiveSchoolId))
            throw new KeyNotFoundException("تجربة الزيارات V2 غير مفعلة لهذه المدرسة.");
    }

    private void EnsureEditable(Visit visit)
    {
        var creatorCanEdit = visit.CreatedByUserId == currentUser.UserId &&
                             visit.Status is VisitStatus.Draft or VisitStatus.RejectedForChanges or VisitStatus.Reopened;
        var managerCanEdit = IsManager() &&
                             visit.Status is (VisitStatus.Draft or VisitStatus.RejectedForChanges or VisitStatus.Reopened or VisitStatus.PendingApproval);
        if (!creatorCanEdit && !managerCanEdit)
            throw new BusinessRuleException("لا يمكن تعديل الزيارة في حالتها الحالية.");
    }

    private bool IsModeratorOnly() => currentUser.IsInRole(RoleNames.Moderator) && !IsManager();
    private bool IsInstructorOnly() => currentUser.IsInRole(RoleNames.Instructor) && !IsManager() && !IsModeratorOnly();
    private bool IsManager() => currentUser.IsGlobalAdmin() || currentUser.IsInRole(RoleNames.SchoolManager);

    private static void ValidateMetadata(int period, int present, int absent)
    {
        if (period is < 1 or > 7) throw new ArgumentException("رقم الحصة يجب أن يكون بين 1 و7.");
        if (present < 0 || absent < 0) throw new ArgumentException("أعداد الحضور والغياب لا يمكن أن تكون سالبة.");
    }

    private static void ValidateEnums(int category, int sequence)
    {
        if (!Enum.IsDefined(typeof(VisitCategory), category)) throw new ArgumentException("فئة الزيارة غير صالحة.");
        if (!Enum.IsDefined(typeof(VisitSequence), sequence)) throw new ArgumentException("تسلسل الزيارة غير صالح.");
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static VisitV2DetailDto MapDetail(Visit visit)
    {
        var scores = visit.Scores.OrderBy(s => s.RubricStandard.Domain.SortOrder).ThenBy(s => s.RubricStandard.SortOrder).ToList();
        var domains = scores.GroupBy(s => s.RubricStandard.Domain)
            .Select(g => new VisitV2DomainDto(g.Key.Id, g.Key.Code, g.Key.NameAr, g.Key.SortOrder,
                g.Select(s => new VisitV2StandardDto(
                    s.RubricStandardId, s.RubricStandard.Code, s.RubricStandard.TextAr,
                    s.RubricStandard.SortOrder, s.Score ?? 1, s.EvidenceNote,
                    s.RubricStandard.Indicators.OrderBy(i => i.SortOrder)
                        .Select(i => new VisitV2IndicatorDto(i.Id, i.Code, i.TextAr, i.SortOrder,
                            s.ObservedIndicators.Any(o => !o.IsDeleted && o.RubricIndicatorId == i.Id))).ToList())).ToList()))
            .OrderBy(d => d.SortOrder).ToList();

        VisitV2AnalysisDto? analysis = null;
        if (visit.Analysis is { RuleSetVersion: 2 } stored)
        {
            var calculated = VisitV2AnalysisEngine.Calculate(scores.Select(s => new StandardScoreV2Input
            {
                DomainId = s.RubricStandard.Domain.Id,
                DomainName = s.RubricStandard.Domain.NameAr,
                StandardId = s.RubricStandardId,
                StandardText = s.RubricStandard.TextAr,
                Score = s.Score ?? 1
            }).ToList());
            analysis = new VisitV2AnalysisDto(
                (int)stored.TotalScore, (int)stored.MaximumScore, stored.OverallPercentage ?? calculated.OverallPercent,
                stored.PerformanceLevelAr,
                calculated.DomainScores.Select(d => new VisitV2DomainAnalysisDto(
                    d.DomainId, scores.First(s => s.RubricStandard.Domain.Id == d.DomainId).RubricStandard.Domain.Code,
                    d.DomainName, d.Sum, d.MaxScore, d.Percent, d.Level, d.IsStrength, d.IsWeakness)).ToList(),
                calculated.Strengths,
                calculated.Weaknesses.Select(w => $"{w.DomainName} ({w.Percent}%)").ToList(),
                stored.ComputedAt);
        }

        return new VisitV2DetailDto(
            visit.Id, visit.SchoolId, visit.School.Name, visit.InstructorId, visit.Instructor.FullName,
            visit.EvaluatorNameSnapshot ?? visit.CreatedByUser.FullName, visit.EvaluatorRoleSnapshot ?? string.Empty,
            (int)visit.VisitCategory, visit.VisitCategory.ToArabicString(), (int)visit.VisitSequence,
            visit.VisitSequence.ToArabicString(), (int)visit.Status, StatusLabel(visit.Status), visit.VisitDate,
            visit.ClassroomPeriod ?? 0, visit.Subject ?? string.Empty, visit.GradeClass ?? string.Empty,
            visit.LessonTitle ?? string.Empty, visit.PresentCount, visit.AbsentCount, visit.Notes,
            visit.RubricVersionId, domains, analysis,
            visit.TreatmentSnapshots.Where(t => !t.IsDeleted).OrderBy(t => t.SortOrder)
                .Select(t => new VisitV2TreatmentDto(t.Id, t.RubricDomainId, t.DomainNameArSnapshot, t.Goal,
                    t.Actions, t.SuccessIndicators, (int)t.Source, t.SortOrder)).ToList(),
            visit.CreatedAt, visit.UpdatedAt, visit.SubmittedAt, visit.ApprovedAt, visit.RejectionReason,
            visit.ReopenReason, visit.Status is not (VisitStatus.Draft or VisitStatus.RejectedForChanges or VisitStatus.Reopened));
    }

    private static string StatusLabel(VisitStatus status) => status switch
    {
        VisitStatus.Draft => "مسودة",
        VisitStatus.PendingApproval => "بانتظار الاعتماد",
        VisitStatus.Approved => "معتمدة",
        VisitStatus.RejectedForChanges => "مرفوضة للتعديل",
        VisitStatus.Reopened => "مُعاد فتحها",
        VisitStatus.UnderReviewAfterComplaint => "قيد المراجعة بعد شكوى",
        VisitStatus.Cancelled => "ملغاة",
        _ => "مُرسلة"
    };

    private static readonly IReadOnlyList<VisitV2ScoreLabelDto> ScoreLabels = new[]
    {
        new VisitV2ScoreLabelDto(1, "منخفضة"),
        new VisitV2ScoreLabelDto(2, "متوسطة"),
        new VisitV2ScoreLabelDto(3, "مرتفعة"),
        new VisitV2ScoreLabelDto(4, "مرتفعة جداً")
    };
}
