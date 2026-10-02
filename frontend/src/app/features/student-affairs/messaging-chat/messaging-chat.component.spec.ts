import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { ClassroomPage, StudentStatsPage } from '../../../core/models/daily-operations.models';
import { ConversationDto, ConversationMessageDto, ReferralDto, SendMessageResultDto } from '../../../core/models/phase5.models';
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
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<Phase5Service>('Phase5Service', [
      'listConversations', 'getGuardianTeacherOptions', 'getGuardianStaffOptions',
      'getStudentGuardianOptions', 'getStudentGuardians', 'listReferrals',
      'createConversation', 'createIdempotencyKey', 'sendMessage', 'closeConversation'
    ]);
    api.listConversations.and.returnValue(of(success(page<ConversationDto>())));
    api.getGuardianTeacherOptions.and.returnValue(of(success([
      { instructorProfileId: 31, displayName: 'المعلم أحمد', subject: 'الرياضيات' }
    ])));
    api.getGuardianStaffOptions.and.returnValue(of(success([])));
    api.getStudentGuardians.and.returnValue(of(success([])));
    api.listReferrals.and.returnValue(of(success<PagedResult<ReferralDto>>(page<ReferralDto>())));
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
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'warn', 'info']);

    await TestBed.configureTestingModule({
      imports: [MessagingChatComponent, NoopAnimationsModule],
      providers: [
        { provide: Phase5Service, useValue: api },
        { provide: StudentAffairsDashboardService, useValue: dashboard },
        { provide: DailyOperationsService, useValue: directory },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast }
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

  it('derives delivered and read labels from receipt timestamps, not from a cosmetic message flag', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    const base = {
      id: 11,
      conversationId: 2,
      sender: { userId: 'guardian-1', displayName: 'ولي الأمر', roleSnapshot: 'Guardian' },
      body: 'رسالة متابعة',
      replyToMessageId: null,
      createdAt: '2026-09-30T08:00:00Z',
      deliveryState: 'Delivered',
      disposition: 'SentImmediately',
      nextEligibleSendAt: null
    } as const;

    const delivered = component.receiptView({ ...base, receipts: [{ recipientLabel: 'شؤون الطلاب', recipientRole: 'StudentAffairsOfficer', status: 'Delivered', deliveredAt: '2026-09-30T08:01:00Z', readAt: null }] } as ConversationMessageDto);
    const read = component.receiptView({ ...base, receipts: [{ recipientLabel: 'شؤون الطلاب', recipientRole: 'StudentAffairsOfficer', status: 'Delivered', deliveredAt: '2026-09-30T08:01:00Z', readAt: '2026-09-30T08:03:00Z' }] } as ConversationMessageDto);

    expect(delivered.label).toBe('تم الاستلام');
    expect(delivered.time).toBe('2026-09-30T08:01:00Z');
    expect(read.label).toBe('تمت القراءة');
    expect(read.time).toBe('2026-09-30T08:03:00Z');
  });

  it('uses the row version returned after sending when the conversation is closed', () => {
    const open = conversation(9, 'old-version');
    const message = {
      id: 91,
      conversationId: open.id,
      sender: { userId: 'guardian-1', displayName: 'ولي الأمر', roleSnapshot: 'Guardian' },
      body: 'تمت المتابعة',
      replyToMessageId: null,
      createdAt: '2026-10-02T08:00:00Z',
      deliveryState: 'Delivered',
      disposition: 'SentImmediately',
      nextEligibleSendAt: null,
      receipts: []
    } as ConversationMessageDto;
    api.sendMessage.and.returnValue(of(success<SendMessageResultDto>({
      message,
      disposition: 'SentImmediately',
      nextEligibleSendAt: null,
      conversationRowVersion: 'new-version'
    })));
    api.closeConversation.and.returnValue(of(success<ConversationDto>({ ...open, status: 'Closed', rowVersion: 'closed-version' })));
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    component.selected.set(open);
    component.draft.setValue('تمت المتابعة');

    component.send();
    component.closeReason.setValue('اكتملت المتابعة');
    component.closeThread();

    expect(component.selected()?.rowVersion).toBe('closed-version');
    expect(api.closeConversation).toHaveBeenCalledOnceWith(9, jasmine.objectContaining({ rowVersion: 'new-version' }));
  });

  it('accepts a guardian message outside office hours, clears the draft and shows an Arabic notice beside it', () => {
    const fixture = TestBed.createComponent(MessagingChatComponent);
    const component = fixture.componentInstance;
    // Render this selected thread without starting inbox polling.
    spyOn(component, 'ngOnInit');
    component.loadingThread.set(false);
    component.selected.set({ ...conversation(9, 'v1'), threadType: 'GuardianTeacher' });
    api.sendMessage.and.returnValue(of(success<SendMessageResultDto>({
      message: queuedMessage(), disposition: 'QueuedUntilOfficeHours',
      nextEligibleSendAt: queuedMessage().nextEligibleSendAt, conversationRowVersion: 'v2'
    })));
    component.draft.setValue('متابعة مستوى الطالب');

    component.send();
    fixture.detectChanges();

    expect(api.sendMessage).toHaveBeenCalledOnceWith(9, jasmine.objectContaining({ body: 'متابعة مستوى الطالب' }));
    expect(component.draft.value).toBe('');
    expect(component.sendError()).toBe('');
    const notice: HTMLElement = fixture.nativeElement.querySelector('article.message .queued-warning');
    expect(notice.textContent).toContain('أرسلت هذه الرسالة خارج الساعات المكتبية');
    expect(notice.textContent).toContain('أقرب ساعات مكتبية وموعد الرد المتوقع');
    expect(notice.textContent).toContain('الأحد');
    expect(notice.textContent).toContain('٧:٥٠');
    expect(notice.textContent).toContain('بتوقيت المدرسة');
    expect(toast.info).toHaveBeenCalledOnceWith('أرسلت رسالتك خارج الساعات المكتبية', jasmine.any(String));
  });

  it('restores the office hours notice from the persisted message after reload', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    expect(component.officeHoursNotice(queuedMessage())).toContain('موعد الرد المتوقع');
    expect(component.officeHoursNotice({ ...queuedMessage(), nextEligibleSendAt: '2026-10-05T09:15:00+03:00' }))
      .toContain('٩:١٥');
  });

  it('does not invent a reply time when no eligible office hours are configured', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    expect(component.officeHoursNotice({ ...queuedMessage(), nextEligibleSendAt: null }))
      .toBe('لم يُحدد موعد للساعات المكتبية بعد؛ لا يمكن تحديد موعد الرد المتوقع حاليًا.');
  });

  it('removes the pending office hours notice once the message is delivered', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    expect(component.officeHoursNotice({ ...queuedMessage(), deliveryState: 'Delivered', nextEligibleSendAt: null })).toBeNull();
    expect(component.officeHoursNotice({ ...queuedMessage(), disposition: 'SentImmediately' })).toBeNull();
  });

  it('shows the notice only to the guardian who sent the message', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    expect(component.officeHoursNotice({ ...queuedMessage(), sender: { userId: 'other-guardian', displayName: 'ولي أمر', roleSnapshot: 'Guardian' } })).toBeNull();
    for (const role of ['Instructor', 'StudentAffairsOfficer', 'SocialWorker']) {
      auth.hasRole.and.callFake(candidate => candidate === role);
      expect(component.officeHoursNotice(queuedMessage())).toBeNull();
    }
  });

  it('groups every old and open thread under the same person', () => {
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;
    const first = conversation(1, 'v1');
    const second = { ...conversation(2, 'v2'), status: 'Closed' as const, subject: 'موضوع سابق' };
    component.conversations.set([first, second]);

    expect(component.contacts()).toHaveSize(1);
    expect(component.contacts()[0].conversations.map(item => item.id)).toEqual([1, 2]);
    expect(component.contacts()[0].openCount).toBe(1);
  });

  it('lets the assigned social worker start a guardian conversation from an active referral', () => {
    auth.hasRole.and.callFake(role => role === 'SocialWorker');
    api.listReferrals.and.returnValue(of(success({
      ...page(),
      items: [{
        id: 44,
        student: { id: 19, studentNumber: 'S-19', displayName: 'الطالب التجريبي', classroomId: 2, classLabel: '1/A', isActive: true, photoUrl: null },
        status: 'InProgress'
      } as any]
    })));
    api.getStudentGuardians.and.returnValue(of(success([{
      id: 7,
      guardian: { id: 72, displayName: 'ولي أمر الطالب', relationship: 'Father', isPrimary: true, receivesNotifications: true },
      canSubmitExcuses: true,
      canRequestGatePass: true,
      validFrom: '2026-09-01',
      validTo: null,
      isActive: true,
      rowVersion: 'link-version'
    }] as const)));
    api.createConversation.and.returnValue(throwError(() => new Error('stop after request capture')));
    const component = TestBed.createComponent(MessagingChatComponent).componentInstance;

    component.openCreateDialog();
    component.createReferralId.setValue(44);
    component.onSocialWorkerReferralChanged();
    component.createRecipientKey.setValue('guardian:72');
    component.createSubject.setValue('متابعة الحالة');
    component.createBody.setValue('نرغب في متابعة حالة الطالب.');
    component.createConversation();

    expect(api.createConversation).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      studentId: 19,
      referralId: 44,
      threadType: 'GuardianSocialWorker',
      targetGuardianProfileId: 72
    }));
  });
});

function conversation(id: number, rowVersion: string): ConversationDto {
  return {
    id,
    student: { id: 5, studentNumber: 'S-5', displayName: 'Student', classroomId: 2, classLabel: '1/A', isActive: true, photoUrl: null },
    subject: `Thread ${id}`,
    threadType: 'GuardianSocialWorker',
    status: 'Open',
    participants: [
      { userId: 'guardian-1', displayName: 'ولي الأمر', role: 'Guardian' },
      { userId: 'worker-1', displayName: 'الموجه الطلابي', role: 'SocialWorker' }
    ],
    unreadCount: 0,
    updatedAt: '2026-10-02T07:00:00Z',
    rowVersion,
    referralId: 44
  };
}

function queuedMessage(): ConversationMessageDto {
  return {
    id: 91, conversationId: 9,
    sender: { userId: 'guardian-1', displayName: 'ولي الأمر', roleSnapshot: 'Guardian' },
    body: 'متابعة مستوى الطالب', replyToMessageId: null, createdAt: '2026-10-03T20:00:00Z',
    deliveryState: 'Pending', disposition: 'QueuedUntilOfficeHours',
    nextEligibleSendAt: '2026-10-04T07:50:00+03:00', receipts: []
  };
}

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
