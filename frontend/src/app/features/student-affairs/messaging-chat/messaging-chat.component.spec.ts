import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { ClassroomPage, StudentStatsPage } from '../../../core/models/daily-operations.models';
import { ConversationDto } from '../../../core/models/phase5.models';
import { GuardianStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { ToastService } from '../../../core/services/toast.service';
import { MessagingChatComponent } from './messaging-chat.component';

describe('MessagingChatComponent Guardian conversation wizard', () => {
  let api: jasmine.SpyObj<Phase5Service>;
  let dashboard: jasmine.SpyObj<StudentAffairsDashboardService>;
  let directory: jasmine.SpyObj<DailyOperationsService>;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<Phase5Service>('Phase5Service', [
      'listConversations', 'getGuardianTeacherOptions', 'getGuardianStaffOptions',
      'getStudentGuardianOptions', 'createConversation', 'createIdempotencyKey'
    ]);
    api.listConversations.and.returnValue(of(success(page<ConversationDto>())));
    api.getGuardianTeacherOptions.and.returnValue(of(success([
      { instructorProfileId: 31, displayName: 'المعلم أحمد', subject: 'الرياضيات' }
    ])));
    api.getGuardianStaffOptions.and.returnValue(of(success([])));
    api.createIdempotencyKey.and.returnValue('stable-create-key');

    dashboard = jasmine.createSpyObj<StudentAffairsDashboardService>('StudentAffairsDashboardService', ['getGuardianStudents']);
    dashboard.getGuardianStudents.and.returnValue(of(success([guardianStudent()] as const)));

    directory = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', ['getClassrooms', 'getStudentsStats']);
    directory.getClassrooms.and.returnValue(of(success<ClassroomPage>(page())));
    directory.getStudentsStats.and.returnValue(of(success<StudentStatsPage>({ ...page(), totalClassrooms: 0 })));

    auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'hasPermission', 'currentUser']);
    auth.hasRole.and.callFake(role => role === 'Guardian');
    auth.hasPermission.and.returnValue(true);
    auth.currentUser.and.returnValue({ userId: 'guardian-1' } as any);

    await TestBed.configureTestingModule({
      imports: [MessagingChatComponent, NoopAnimationsModule],
      providers: [
        { provide: Phase5Service, useValue: api },
        { provide: StudentAffairsDashboardService, useValue: dashboard },
        { provide: DailyOperationsService, useValue: directory },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'warn', 'info']) }
      ]
    }).compileComponents();
  });

  it('builds recipient choices only from the safe teacher lookup', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    component.openCreateDialog();
    component.createThreadType.setValue('GuardianTeacher');

    component.onConversationScopeChanged();

    expect(api.getGuardianTeacherOptions).toHaveBeenCalledOnceWith(5);
    expect(component.recipientOptions()).toEqual([jasmine.objectContaining({
      key: 'teacher:31',
      instructorProfileId: 31,
      staffUserId: null
    }) as any]);
  });

  it('keeps the same idempotency key when a retryable create request fails', () => {
    api.createConversation.and.returnValue(throwError(() => new Error('network')));
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    component.openCreateDialog();
    component.createThreadType.setValue('GuardianTeacher');
    component.onConversationScopeChanged();
    component.createRecipientKey.setValue('teacher:31');
    component.createSubject.setValue('متابعة دراسية');
    component.createBody.setValue('أرجو التواصل بخصوص المستوى.');

    component.createConversation();
    component.createConversation();

    expect(api.createConversation).toHaveBeenCalledTimes(2);
    expect(api.createConversation.calls.allArgs()[0][0].idempotencyKey).toBe('stable-create-key');
    expect(api.createConversation.calls.allArgs()[1][0].idempotencyKey).toBe('stable-create-key');
    expect(api.createIdempotencyKey).toHaveBeenCalledTimes(1);
    expect(component.createBody.value).toBe('أرجو التواصل بخصوص المستوى.');
  });

  it('builds an officer-to-guardian request from the safe guardian lookup', () => {
    auth.hasRole.and.callFake(role => role === 'StudentAffairsOfficer');
    api.getStudentGuardianOptions.and.returnValue(of(success([
      { guardianProfileId: 72, displayName: 'ولي أمر الطالب', relationship: 'Father', isPrimary: true }
    ])));
    api.createConversation.and.returnValue(throwError(() => new Error('stop after request capture')));
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;

    component.openCreateDialog();
    expect(component.createThreadType.value).toBe('GuardianStudentAffairs');
    component.createStudentId.setValue(19);
    component.onConversationScopeChanged();
    component.createRecipientKey.setValue('guardian:72');
    component.createSubject.setValue('متابعة انتظام الطالب');
    component.createBody.setValue('نرغب في التواصل مع ولي الأمر للمتابعة.');
    component.createConversation();

    expect(api.getStudentGuardianOptions).toHaveBeenCalledOnceWith(19);
    expect(api.createConversation).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      studentId: 19,
      threadType: 'GuardianStudentAffairs',
      targetGuardianProfileId: 72,
      targetInstructorProfileId: null,
      targetStaffRole: null,
      targetStaffUserId: null
    }));
  });
});

function guardianStudent(): GuardianStudentDto {
  return {
    student: { id: 5, studentNumber: 'S-5', displayName: 'Student', classroomId: 2, classLabel: '1/A', isActive: true, photoUrl: null },
    canSubmitExcuses: true,
    canRequestGatePass: true,
    receivesNotifications: true
  };
}

function page<T>(): PagedResult<T> {
  return { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0, hasNext: false, hasPrevious: false };
}

function success<T>(data: T): ApiResponse<T> {
  return { isSuccess: true, message: '', errors: [], data };
}
