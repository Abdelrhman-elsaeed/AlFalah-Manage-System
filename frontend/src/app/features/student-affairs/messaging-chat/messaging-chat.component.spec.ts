import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { ConversationDto } from '../../../core/models/phase5.models';
import { GuardianStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { ToastService } from '../../../core/services/toast.service';
import { MessagingChatComponent } from './messaging-chat.component';

describe('MessagingChatComponent Guardian conversation wizard', () => {
  let api: jasmine.SpyObj<Phase5Service>;
  let dashboard: jasmine.SpyObj<StudentAffairsDashboardService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<Phase5Service>('Phase5Service', [
      'listConversations', 'getGuardianTeacherOptions', 'getGuardianStaffOptions',
      'createConversation', 'createIdempotencyKey'
    ]);
    api.listConversations.and.returnValue(of(success(page<ConversationDto>())));
    api.getGuardianTeacherOptions.and.returnValue(of(success([
      { instructorProfileId: 31, displayName: 'المعلم أحمد', subject: 'الرياضيات' }
    ])));
    api.getGuardianStaffOptions.and.returnValue(of(success([])));
    api.createIdempotencyKey.and.returnValue('stable-create-key');

    dashboard = jasmine.createSpyObj<StudentAffairsDashboardService>('StudentAffairsDashboardService', ['getGuardianStudents']);
    dashboard.getGuardianStudents.and.returnValue(of(success([guardianStudent()] as const)));

    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'hasPermission', 'currentUser']);
    auth.hasRole.and.callFake(role => role === 'Guardian');
    auth.hasPermission.and.returnValue(true);
    auth.currentUser.and.returnValue({ userId: 'guardian-1' } as any);

    await TestBed.configureTestingModule({
      imports: [MessagingChatComponent, NoopAnimationsModule],
      providers: [
        { provide: Phase5Service, useValue: api },
        { provide: StudentAffairsDashboardService, useValue: dashboard },
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
