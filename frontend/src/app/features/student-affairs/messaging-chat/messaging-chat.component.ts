import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { EMPTY, filter, finalize, forkJoin, fromEvent, switchMap, timer } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  ConversationDto,
  ConversationMessageDto,
  ConversationThreadType,
  CreateConversationRequestDto,
  SendMessageResultDto,
  StudentGuardianOptionDto
} from '../../../core/models/phase5.models';
import { ClassroomDto, StudentStatsDto } from '../../../core/models/daily-operations.models';
import { GuardianStudentDto } from '../../../core/models/student-affairs-dashboard.models';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { StudentAffairsDashboardService } from '../../../core/services/student-affairs-dashboard.service';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { ToastService } from '../../../core/services/toast.service';

interface ConversationRecipientOption {
  readonly key: string;
  readonly label: string;
  readonly threadType: ConversationThreadType;
  readonly instructorProfileId: number | null;
  readonly staffUserId: string | null;
  readonly staffRole: string | null;
  readonly guardianProfileId: number | null;
}

@Component({
  selector: 'app-messaging-chat',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, DialogModule, DropdownModule, InputTextModule, InputTextareaModule, ProgressSpinnerModule, TagModule],
  templateUrl: './messaging-chat.component.html',
  styleUrl: './messaging-chat.component.css'
})
export class MessagingChatComponent implements OnInit {
  private readonly api = inject(Phase5Service);
  private readonly dashboardApi = inject(StudentAffairsDashboardService);
  private readonly directoryApi = inject(DailyOperationsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly conversations = signal<readonly ConversationDto[]>([]);
  readonly totalRecords = signal(0);
  readonly loadingInbox = signal(true);
  readonly inboxError = signal('');
  readonly unreadOnly = signal(false);
  readonly conversationSearch = new FormControl('', { nonNullable: true });
  private readonly conversationSearchTerm = signal('');
  readonly filteredConversations = computed(() => {
    const term = this.conversationSearchTerm().trim().toLocaleLowerCase('ar');
    if (!term) return this.conversations();
    return this.conversations().filter(item =>
      item.subject.toLocaleLowerCase('ar').includes(term)
      || item.student.displayName.toLocaleLowerCase('ar').includes(term)
      || item.student.studentNumber.toLocaleLowerCase('ar').includes(term));
  });
  readonly unreadCount = computed(() => this.conversations().reduce((total, item) => total + item.unreadCount, 0));
  readonly openCount = computed(() => this.conversations().filter(item => item.status === 'Open').length);
  readonly selected = signal<ConversationDto | null>(null);
  readonly messages = signal<readonly ConversationMessageDto[]>([]);
  readonly loadingThread = signal(false);
  readonly loadingOlder = signal(false);
  readonly hasOlder = signal(false);
  readonly sending = signal(false);
  readonly sendError = signal('');
  readonly pendingIdempotencyKey = signal<string | null>(null);
  readonly queuedResults = signal<ReadonlyMap<number, SendMessageResultDto>>(new Map<number, SendMessageResultDto>());
  readonly draft = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  readonly closeDialogVisible = signal(false);
  readonly closeReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });
  readonly closing = signal(false);
  readonly createDialogVisible = signal(false);
  readonly guardianStudents = signal<readonly GuardianStudentDto[]>([]);
  readonly officerClassrooms = signal<readonly ClassroomDto[]>([]);
  readonly officerStudents = signal<readonly StudentStatsDto[]>([]);
  readonly loadingOfficerDirectory = signal(false);
  readonly recipientOptions = signal<readonly ConversationRecipientOption[]>([]);
  readonly loadingCreateScope = signal(false);
  readonly createError = signal('');
  readonly creatingConversation = signal(false);
  readonly createStudentId = new FormControl<number | null>(null, Validators.required);
  readonly createClassroomId = new FormControl<number | null>(null);
  readonly createThreadType = new FormControl<ConversationThreadType | null>(null, Validators.required);
  readonly createRecipientKey = new FormControl<string | null>(null, Validators.required);
  readonly createSubject = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] });
  readonly createBody = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  readonly quickReplies = [
    'تمت مراجعة الحالة، وسنوافيكم بالتحديث فور اكتمال الإجراء.',
    'شكرًا لتواصلكم. نرجو تزويدنا بالمستند أو التفصيل المطلوب للمتابعة.',
    'تم استلام رسالتكم وتحويلها للجهة المختصة داخل المدرسة.',
    'نرجو تأكيد استلام الرسالة، ويمكنكم الرد هنا مباشرة عند وجود أي استفسار.'
  ] as const;
  private pendingCreateAttempt: { readonly fingerprint: string; readonly key: string } | null = null;

  get currentUserId(): string { return this.auth.currentUser()?.userId ?? ''; }
  get canClose(): boolean { return this.auth.hasPermission('Messaging.CloseThread'); }
  get isGuardian(): boolean { return this.auth.hasRole('Guardian'); }
  get isOfficer(): boolean { return this.auth.hasRole('StudentAffairsOfficer'); }
  get canStartConversation(): boolean {
    return (this.isGuardian && (this.auth.hasPermission('Messaging.StartGuardianTeacher')
      || this.auth.hasPermission('Messaging.StartGuardianAdministration')))
      || (this.isOfficer && this.auth.hasPermission('Messaging.StartOfficerGuardian'));
  }
  get threadTypeOptions(): readonly { label: string; value: ConversationThreadType }[] {
    const options: { label: string; value: ConversationThreadType }[] = [];
    if (this.auth.hasPermission('Messaging.StartGuardianTeacher'))
      options.push({ label: 'معلم الابن', value: 'GuardianTeacher' });
    if (this.auth.hasPermission('Messaging.StartGuardianAdministration')) {
      options.push({ label: 'شؤون الطلاب', value: 'GuardianStudentAffairs' });
      options.push({ label: 'الموجّه الطلابي', value: 'GuardianSocialWorker' });
    }
    return options;
  }

  ngOnInit(): void {
    this.loadInbox();
    this.conversationSearch.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(value => this.conversationSearchTerm.set(value));
    timer(25_000, 25_000).pipe(
      filter(() => typeof document === 'undefined' || document.visibilityState === 'visible'),
      switchMap(() => {
        const thread = this.selected();
        return thread ? this.api.getMessages(thread.id, 50) : EMPTY;
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(response => {
      if (response.isSuccess && response.data) this.mergeMessages(response.data.items);
    });
    if (typeof window !== 'undefined') {
      fromEvent(window, 'focus').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.revalidateOpenThread());
    }
  }

  loadInbox(): void {
    this.loadingInbox.set(true);
    this.inboxError.set('');
    this.api.listConversations({ pageNumber: 1, pageSize: 100, isUnread: this.unreadOnly() || undefined }).pipe(finalize(() => this.loadingInbox.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.inboxError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل المحادثات.'); return; }
        this.conversations.set(response.data.items);
        this.totalRecords.set(response.data.totalCount);
        const selectedId = this.selected()?.id;
        if (selectedId) {
          const latest = response.data.items.find(item => item.id === selectedId);
          if (latest) this.selected.set(latest);
          else if (response.data.items.length) this.selectConversation(response.data.items[0]);
        } else if (response.data.items.length) {
          this.selectConversation(response.data.items[0]);
        }
      },
      error: error => this.inboxError.set(this.httpMessage(error, 'تعذر تحميل المحادثات.'))
    });
  }

  openCreateDialog(): void {
    this.createDialogVisible.set(true);
    this.createError.set('');
    this.recipientOptions.set([]);
    this.createStudentId.reset(null);
    this.createClassroomId.reset(null);
    this.createThreadType.reset(null);
    this.createRecipientKey.reset(null);
    this.createSubject.reset('');
    this.createBody.reset('');
    this.pendingCreateAttempt = null;
    if (this.isOfficer) {
      this.createThreadType.setValue('GuardianStudentAffairs');
      this.loadOfficerClassrooms();
    } else {
      this.loadGuardianStudents();
    }
  }

  onOfficerClassroomChanged(): void {
    this.createStudentId.reset(null);
    this.createRecipientKey.reset(null);
    this.recipientOptions.set([]);
    const classroomId = this.createClassroomId.value;
    if (!classroomId) { this.officerStudents.set([]); return; }
    this.loadingOfficerDirectory.set(true);
    this.directoryApi.getStudentsStats({ pageNumber: 1, pageSize: 100, classroomId, isActive: true }).pipe(
      finalize(() => this.loadingOfficerDirectory.set(false))
    ).subscribe({
      next: response => this.officerStudents.set(response.data?.items ?? []),
      error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل طلاب الفصل.'))
    });
  }

  onConversationScopeChanged(): void {
    this.createRecipientKey.reset(null);
    this.recipientOptions.set([]);
    this.createError.set('');
    const studentId = this.createStudentId.value;
    const threadType = this.createThreadType.value;
    if (!studentId || !threadType) return;

    this.loadingCreateScope.set(true);
    if (this.isOfficer) {
      this.api.getStudentGuardianOptions(studentId).pipe(finalize(() => this.loadingCreateScope.set(false))).subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) {
            this.createError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل أولياء الأمور المرتبطين بالطالب.');
            return;
          }
          this.recipientOptions.set(response.data.map(option => ({
            key: `guardian:${option.guardianProfileId}`,
            label: `${option.displayName} — ${this.relationshipLabel(option)}${option.isPrimary ? ' · ولي أساسي' : ''}`,
            threadType: 'GuardianStudentAffairs',
            instructorProfileId: null,
            staffUserId: null,
            staffRole: null,
            guardianProfileId: option.guardianProfileId
          })));
        },
        error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل أولياء الأمور المرتبطين بالطالب.'))
      });
      return;
    }
    if (threadType === 'GuardianTeacher') {
      this.api.getGuardianTeacherOptions(studentId).pipe(finalize(() => this.loadingCreateScope.set(false))).subscribe({
        next: response => {
          if (!response.isSuccess || !response.data) {
            this.createError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل المعلمين المصرح بهم.');
            return;
          }
          this.recipientOptions.set(response.data.map(option => ({
            key: `teacher:${option.instructorProfileId}`,
            label: `${option.displayName} — ${option.subject}`,
            threadType,
            instructorProfileId: option.instructorProfileId,
            staffUserId: null,
            staffRole: null,
            guardianProfileId: null
          })));
        },
        error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل المعلمين المصرح بهم.'))
      });
      return;
    }

    this.api.getGuardianStaffOptions(studentId).pipe(finalize(() => this.loadingCreateScope.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.createError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل المستلمين المصرح بهم.');
          return;
        }
        this.recipientOptions.set(response.data.filter(option => option.threadType === threadType).map(option => ({
            key: `staff:${option.userId}:${option.role}`,
            label: `${option.displayName} — ${option.role}`,
            threadType,
            instructorProfileId: null,
            staffUserId: option.userId,
            staffRole: option.role,
            guardianProfileId: null
          })));
      },
      error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل المستلمين المصرح بهم.'))
    });
  }

  createConversation(): void {
    const studentId = this.createStudentId.value;
    const threadType = this.createThreadType.value;
    const recipient = this.recipientOptions().find(option => option.key === this.createRecipientKey.value);
    const subject = this.createSubject.value.trim();
    const initialBody = this.createBody.value.trim();
    if (!studentId || !threadType || !recipient || !subject || !initialBody || this.creatingConversation()) return;

    const fingerprint = JSON.stringify({ studentId, threadType, recipient: recipient.key, subject, initialBody });
    const idempotencyKey = this.pendingCreateAttempt?.fingerprint === fingerprint
      ? this.pendingCreateAttempt.key
      : this.api.createIdempotencyKey();
    this.pendingCreateAttempt = { fingerprint, key: idempotencyKey };
    const request: CreateConversationRequestDto = {
      studentId,
      threadType,
      targetInstructorProfileId: recipient.instructorProfileId,
      targetStaffRole: recipient.staffRole,
      targetStaffUserId: recipient.staffUserId,
      subject,
      initialBody,
      idempotencyKey,
      targetGuardianProfileId: recipient.guardianProfileId
    };
    this.creatingConversation.set(true);
    this.createError.set('');
    this.api.createConversation(request).pipe(finalize(() => this.creatingConversation.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.createError.set(response.errors[0] ?? response.message ?? 'لم يتم إنشاء المحادثة.');
          return;
        }
        this.pendingCreateAttempt = null;
        this.createDialogVisible.set(false);
        const alreadyListed = this.conversations().some(item => item.id === response.data!.id);
        this.conversations.update(items => [response.data!, ...items.filter(item => item.id !== response.data!.id)]);
        if (!alreadyListed) this.totalRecords.update(total => total + 1);
        this.selectConversation(response.data);
        this.toast.success('تم إنشاء المحادثة', 'تم حفظ الرسالة الأولى وفتح المحادثة.');
      },
      error: error => this.createError.set(this.httpMessage(error, 'تعذر تأكيد إنشاء المحادثة. أعد المحاولة بنفس البيانات لتجنب التكرار.'))
    });
  }

  selectConversation(item: ConversationDto): void {
    this.selected.set(item);
    this.messages.set([]);
    this.hasOlder.set(false);
    this.sendError.set('');
    this.loadingThread.set(true);
    forkJoin({ header: this.api.getConversation(item.id), messages: this.api.getMessages(item.id, 50) }).pipe(finalize(() => this.loadingThread.set(false))).subscribe({
      next: ({ header, messages }) => {
        if (!header.isSuccess || !header.data || !messages.isSuccess || !messages.data) {
          this.toast.error('تعذر فتح المحادثة', header.errors[0] ?? messages.errors[0] ?? header.message ?? messages.message);
          return;
        }
        this.selected.set(header.data);
        this.messages.set(this.sortedUnique(messages.data.items));
        this.hasOlder.set(messages.data.hasNext || messages.data.hasPrevious || messages.data.totalCount > messages.data.items.length);
        this.markRenderedRead();
      },
      error: error => this.toast.error('تعذر فتح المحادثة', this.httpMessage(error, 'تحقق من صلاحية الوصول ثم حاول مجددًا.'))
    });
  }

  loadOlder(): void {
    const thread = this.selected();
    const oldest = this.messages()[0];
    if (!thread || !oldest || this.loadingOlder()) return;
    this.loadingOlder.set(true);
    this.api.getMessages(thread.id, 50, oldest.id).pipe(finalize(() => this.loadingOlder.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.messages.set(this.sortedUnique([...response.data.items, ...this.messages()]));
        this.hasOlder.set(response.data.items.length > 0 && response.data.totalCount > response.data.items.length);
        this.markRenderedRead();
      },
      error: error => this.toast.error('تعذر تحميل الرسائل الأقدم', this.httpMessage(error, 'حاول مرة أخرى.'))
    });
  }

  send(): void {
    const thread = this.selected();
    const body = this.draft.value.trim();
    if (!thread || thread.status !== 'Open' || !body || this.sending()) return;
    const key = this.pendingIdempotencyKey() ?? this.api.createIdempotencyKey();
    this.pendingIdempotencyKey.set(key);
    this.sending.set(true);
    this.sendError.set('');
    this.api.sendMessage(thread.id, { body, replyToMessageId: null, idempotencyKey: key }).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.sendError.set(response.errors[0] ?? response.message ?? 'لم تُرسل الرسالة.'); return; }
        this.appendSendResult(response.data);
        this.pendingIdempotencyKey.set(null);
        this.draft.reset('');
      },
      error: error => this.sendError.set(this.httpMessage(error, 'تعذر تأكيد حفظ الرسالة. ستستخدم إعادة المحاولة نفس مفتاح الإرسال لمنع التكرار.'))
    });
  }

  submitComposer(event: SubmitEvent): void {
    event.preventDefault();
    this.send();
  }

  useQuickReply(reply: string): void {
    const current = this.draft.value.trim();
    this.draft.setValue(current ? `${current}\n${reply}` : reply);
    this.draft.markAsDirty();
  }

  openCloseDialog(): void { this.closeReason.reset(''); this.closeDialogVisible.set(true); }
  closeThread(): void {
    const thread = this.selected();
    const reason = this.closeReason.value.trim();
    if (!thread || !reason || this.closing()) return;
    this.closing.set(true);
    this.api.closeConversation(thread.id, { reason, rowVersion: thread.rowVersion }).pipe(finalize(() => this.closing.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.toast.warn('لم تُغلق المحادثة', response.errors[0] ?? response.message); return; }
        this.acceptConversationUpdate(response.data);
        this.closeDialogVisible.set(false);
        this.toast.success('تم إغلاق المحادثة', 'أصبح صندوق الكتابة للقراءة فقط.');
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) {
          this.toast.warn('تغيرت المحادثة بواسطة مستخدم آخر', 'احتفظنا بسبب الإغلاق وجلبنا أحدث حالة قبل السماح بأي إجراء آخر.');
          this.revalidateHeader();
        } else this.toast.error('تعذر إغلاق المحادثة', this.httpMessage(error, 'حاول مرة أخرى.'));
      }
    });
  }

  isMine(message: ConversationMessageDto): boolean { return message.sender.userId === this.currentUserId; }
  queuedResult(message: ConversationMessageDto): SendMessageResultDto | null {
    return this.queuedResults().get(message.id) ?? (message.disposition === 'QueuedUntilOfficeHours'
      ? { message, disposition: message.disposition, nextEligibleSendAt: message.nextEligibleSendAt }
      : null);
  }
  threadTypeLabel(type: ConversationDto['threadType']): string { return ({ GuardianTeacher: 'ولي الأمر والمعلم', GuardianStudentAffairs: 'ولي الأمر وشؤون الطلاب', GuardianSocialWorker: 'ولي الأمر والموجه الطلابي' })[type]; }
  deliveryLabel(state: ConversationMessageDto['deliveryState']): string { return ({ Pending: 'قيد الانتظار', Delivered: 'تم التسليم', Failed: 'تعذر التسليم' })[state]; }
  formatDateTime(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'short', timeStyle: 'short' }).format(date);
  }

  private appendSendResult(result: SendMessageResultDto): void {
    this.mergeMessages([result.message]);
    this.queuedResults.update(current => {
      const next = new Map(current);
      next.set(result.message.id, result);
      return next;
    });
    if (result.disposition === 'QueuedUntilOfficeHours') {
      this.toast.info('الرسالة مجدولة للساعات المكتبية', result.nextEligibleSendAt ? `سيتم التنبيه في أقرب ساعة مكتبية: ${this.formatDateTime(result.nextEligibleSendAt)}` : 'سيتم تنبيه المعلم خلال فترة مكتبية قادمة.');
    } else if (result.disposition === 'BypassedForUrgency') {
      this.toast.warn('أُرسلت كحالة عاجلة', 'تم تسجيل التجاوز للتدقيق.');
    }
  }
  private loadGuardianStudents(): void {
    if (this.guardianStudents().length) return;
    this.loadingCreateScope.set(true);
    this.dashboardApi.getGuardianStudents().pipe(finalize(() => this.loadingCreateScope.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) {
          this.createError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل الأبناء المرتبطين بحسابك.');
          return;
        }
        this.guardianStudents.set(response.data);
        if (response.data.length === 1) this.createStudentId.setValue(response.data[0].student.id);
      },
      error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل الأبناء المرتبطين بحسابك.'))
    });
  }
  private loadOfficerClassrooms(): void {
    if (this.officerClassrooms().length) return;
    this.loadingOfficerDirectory.set(true);
    this.directoryApi.getClassrooms().pipe(finalize(() => this.loadingOfficerDirectory.set(false))).subscribe({
      next: response => this.officerClassrooms.set((response.data?.items ?? []).filter(item => item.isActive)),
      error: error => this.createError.set(this.httpMessage(error, 'تعذر تحميل الفصول.'))
    });
  }
  private relationshipLabel(option: StudentGuardianOptionDto): string {
    return ({ Father: 'الأب', Mother: 'الأم', Brother: 'الأخ', Sister: 'الأخت', Grandfather: 'الجد', Grandmother: 'الجدة', Uncle: 'العم أو الخال', Aunt: 'العمة أو الخالة', Other: 'صلة أخرى' } as Record<string, string>)[option.relationship] ?? option.relationship;
  }
  private revalidateOpenThread(): void {
    const thread = this.selected();
    if (!thread) { this.loadInbox(); return; }
    forkJoin({ header: this.api.getConversation(thread.id), messages: this.api.getMessages(thread.id, 50) }).subscribe({
      next: ({ header, messages }) => {
        if (header.isSuccess && header.data) this.acceptConversationUpdate(header.data);
        if (messages.isSuccess && messages.data) { this.mergeMessages(messages.data.items); this.markRenderedRead(); }
      }
    });
  }
  private revalidateHeader(): void {
    const thread = this.selected();
    if (!thread) return;
    this.api.getConversation(thread.id).subscribe({ next: response => { if (response.isSuccess && response.data) this.acceptConversationUpdate(response.data); } });
  }
  private acceptConversationUpdate(updated: ConversationDto): void {
    this.selected.set(updated);
    this.conversations.update(items => items.map(item => item.id === updated.id ? updated : item));
  }
  private mergeMessages(incoming: readonly ConversationMessageDto[]): void { this.messages.set(this.sortedUnique([...this.messages(), ...incoming])); }
  private sortedUnique(messages: readonly ConversationMessageDto[]): readonly ConversationMessageDto[] {
    return [...new Map(messages.map(message => [message.id, message])).values()].sort((a, b) => a.id - b.id);
  }
  private markRenderedRead(): void {
    const thread = this.selected();
    const highest = this.messages().at(-1)?.id;
    if (!thread || highest === undefined) return;
    this.api.markConversationRead(thread.id, { throughMessageId: highest }).subscribe({ next: response => { if (response.isSuccess) this.acceptConversationUpdate({ ...thread, unreadCount: 0 }); } });
  }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
}
